using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static vinnysocks5proxy.Helper;
using static trusts.Helper;
using cryptoprime;
using System.Text;
using System.Diagnostics;
using trusts;

namespace vinnysocks5proxy
{
    public partial class ListenConfiguration: IDisposable, IComparable<ListenConfiguration>
    {
        public volatile int connection_count = 0;
    
        public partial class Connection: IDisposable
        {
            // https://datatracker.ietf.org/doc/html/rfc1928#section-3
            protected readonly byte[] ErrorAuthMethodResponse = new byte[] { 0x05, 0xFF };      // X'FF' NO ACCEPTABLE METHODS
            protected readonly byte[] NoAuthMethodResponse    = new byte[] { 0x05, 0x00 };
            protected readonly byte[] PwdAuthMethodResponse   = new byte[] { 0x05, 0x02 };

            // https://datatracker.ietf.org/doc/html/rfc1928#section-6
            // 6.  Replies
            public const int EC_success                       = 0x00;
            public const int EC_general_SOCKS_server_failure  = 0x01;
            public const int EC_Denied                        = 0x02;   // connection not allowed by ruleset
            public const int EC_Network_unreachable           = 0x03;
            public const int EC_Host_unreachable              = 0x04;
            public const int EC_Connection_refused            = 0x05;
            public const int EC_Command_not_supported         = 0x07;
            public const int EC_Address_type_not_supported    = 0x08;
            
            public string connectToSocks = "";
            public Stopwatch start = new Stopwatch();

            protected    byte[] BytesTo   = new byte[BufferSizeForTo];
            protected    byte[] BytesFrom = new byte[BufferSizeForTo];

            public const int    BufferSizeForConnection = 65536;
            public const int    BufferSizeForTo         = 65536;
            public const int    BufferSizeForFrom       = 65536;
            public const int    MaxErrorCount           = 6;

            /// <summary>Используется для кодирования сообщений из октетов в строки</summary>
            public readonly ASCIIEncoding asciiEncoding = new ASCIIEncoding();

            public class MaxConnectinLimitExceedsException: Exception
            {
                public MaxConnectinLimitExceedsException(): base("The max connection limit exceeds")
                {
                }
            }

            // Установка нового соединения клиента с прокси
            public Connection(Socket connection, ListenConfiguration listen)
            {
                this.connection = connection;
                this.listen     = listen;

                connectToSocks = $"{connection.LocalEndPoint.ToString()} <- {connection.RemoteEndPoint.ToString()}";

                var cnt = Interlocked.Increment(ref listen.connection_count);
                if (cnt > listen.max_connections)
                {
                    LogForConnection($"The max connection limit exceeds; Count of connections in the listener {listen.connections.Count} ({listen.connection_count})", connection, 1);
                    Dispose();

                    throw new MaxConnectinLimitExceedsException();
                }

                connection.SendBufferSize    = BufferSizeForConnection;
                connection.ReceiveBufferSize = BufferSizeForConnection;

                start.Start();

                ThreadPool.QueueUserWorkItem
                (
                    delegate
                    {
                        bool successNegotiation = false; 
                        try
                        {
                            var b = BytesTo;
                            var bb = new BytesBuilder();

                            // Установление соединения
                            // Ожидаем байты коннекта по socks5 https://datatracker.ietf.org/doc/html/rfc1928
                            var available = waitAvailableBytes(connection, 3);

                            if (available == 0)
                                return;

                            if (available > b.Length)
                            {
                                available = b.Length;
                            }

                            // На всякий случай, принимаем в цикле, чтобы не пропустить http-заголовок
                            int offset = ReceiveBytes(connection, b);
                            // listen.Log(BitConverter.ToString(b, 0, available));

                            // Версия протокола socks - 0x05
                            // Второй байт - это количество поддерживаемых методов (по байту на метод)
                            // Таким образом, всего сейчас должно быть получено не менее чем methodsCount + 2 байта
                            var methodsCount = b[1];
                            if (b[0] != 0x05 || methodsCount == 0 || methodsCount != offset - 2)
                            {
                                // Проверяяем. Возможно, подключение идёт по http
                                var httpStr = asciiEncoding.GetString(b, 0, offset);
                                if (httpStr.ToLowerInvariant().StartsWith("connect ") && httpStr.EndsWith("\r\n\r\n") && httpStr.ToLowerInvariant().Contains("http/1."))
                                {
                                    if (doHttpConnect(connection, listen, httpStr))
                                        successNegotiation = true;

                                    return;
                                }

                                // Явно, что это не socks5
                                if (httpStr.Length > 17 && httpStr.ToLowerInvariant().Contains("http"))
                                {
                                    // Убираем из строки двоичные данные, чтобы не лезли куда не надо
                                    httpStr = ResetMessageBody(httpStr);

                                    // Это web-прокси без туннелирования с помощью CONNECT
                                    if (httpStr.ToLowerInvariant().Contains("http://")/* || httpStr.ToLowerInvariant().Contains("https://")*/)
                                    {
                                        if (doHttpWithoutConnect(connection, listen, httpStr, b, offset))
                                            successNegotiation = true;

                                        // Делаем здесь выход, чтобы лишний раз не логировать ошибки. Т.к. ниже идёт логирование, которое, скорее всего, выше тоже залогировано
                                        return;
                                    }

                                    if (successNegotiation)
                                        return;

                                    LogForConnection($"incorrect http connection\r\n{httpStr}", connection, 0);
                                    SendHttpResponse("501 Not Implemented", connection);

                                    return;
                                }

                                processInvalidProtocolMessage();
                                return;
                            }

                            // 0 - без аутентификации; 2 - парольная аутентификация
                            int method = listen.users.Count == 0 ? 0 : 2;
                            for (var i = 2; i < available; i++)
                            {
                                if (b[i] == method)
                                    goto findMethod;
                            }

                            // Обработка ошибки
                            // Посылаем сообщение о том, что ни один из методов не подходит
                            connection.Send(ErrorAuthMethodResponse);
                            LogErrorForConnection($"The correct authorization method ({method}) was not found (check user and password in client and in server)", connection, b, available);

                            return;

                        findMethod:

                            if (method == 0)
                                connection.Send(NoAuthMethodResponse);
                            else
                            {
                                // https://datatracker.ietf.org/doc/html/rfc1929
                                // Username/Password Authentication for SOCKS V5
                                // Выдаём клиенту подтверждение аутентификации
                                connection.Send(PwdAuthMethodResponse);
                                // Получаем два первых байта: версию протокола аутентификации, и длину имени пользователя
                                waitAvailableBytes(connection, 2);
                                connection.Receive(b, 2, SocketFlags.None);

                                if (b[0] != 0x01)
                                {
                                    LogErrorForConnection($"Incorrect authentication version (must be 1): {b[0]}", connection, null, 0);
                                    return;
                                }

                                // Получаем имя пользователя
                                var userNameLen = b[1];
                                waitAvailableBytes(connection, userNameLen);
                                connection.Receive(b, userNameLen, SocketFlags.None);
                                var user = asciiEncoding.GetString(b, 0, userNameLen);

                                // Получаем длину пароля и сам пароль
                                waitAvailableBytes(connection, 1);
                                connection.Receive(b, 1, SocketFlags.None);
                                var pwdLen = b[0];
                                connection.Receive(b, pwdLen, SocketFlags.None);

                                var password = asciiEncoding.GetString(b, 0, pwdLen);

                                var key = listen.users.IndexOfKey(user);
                                if (key < 0)
                                {
                                    LogErrorForConnection($"Incorrect user or password: " + user, connection, null, 0);
                                    connection.Send(new byte[] { 0x01, 0x01 });
                                    return;
                                }

                                if (!SecureCompare(password, listen.users[user]))
                                {
                                    LogErrorForConnection($"Incorrect user or password: " + user, connection, null, 0);
                                    connection.Send(new byte[] { 0x01, 0x01 });
                                    return;
                                }

                                // Успешный вход
                                LogForConnection($"Success login for user '" + user + "'", connection, 3);
                                connection.Send(new byte[] { 0x01, 0x00 });
                            }

                            available = waitAvailableBytes(connection, 7);
                            if (available == 0)
                                return;

                            if (available > b.Length)
                            {
                                available = b.Length;
                            }

                            connection.Receive(b, available, SocketFlags.None);
                            // listen.Log(BitConverter.ToString(b, 0, available));

                            // Снова проверяем версию протокола
                            if (b[0] != 0x05)
                            {
                                processInvalidProtocolMessage();
                                return;
                            }

                            // Второй байт - команда соединения
                            // CONNECT '01'
                            // BIND '02'
                            // UDP ASSOCIATE '03'
                            // Мы принимаем только CONNECT
                            if (b[1] != 0x01)
                            {
                                LogErrorForConnection($"vinny-socks5-proxy can accept only 'CONNECT' command, but command " + b[1], connection, b, available);
                                processResponseForRequest(bb, EC_Command_not_supported);
                                return;
                            }

                            // Читаем тип адреса ATYP
                            // Проверяем, что он корректен и разрешён в конфигах
                            var addressType = b[3];
                            if (addressType != 0x01 && addressType != 0x03 && addressType != 0x04)
                            {
                                LogErrorForConnection($"The unknown address type: " + addressType, connection, b, available);
                                processResponseForRequest(bb, EC_Address_type_not_supported);
                                return;
                            }

                            // См. ниже, код сообщения об ошибке дублируется
                            if (addressType == 0x01 && !listen.namesGranted_ipv4)
                            {
                                var str = "";
                                try
                                {
                                    bb.addWithCopy(b, -1, 4, available - 2);
                                    var addrE = new IPAddress(bb.getBytes());
                                    str = " ( " + addrE.ToString() + " )";
                                }
                                finally
                                {
                                    LogErrorForConnection($"ipv4 address is denied by configuration" + str, connection, b, available);
                                }

                                processResponseForRequest(bb, EC_Address_type_not_supported);

                                return;
                            }

                            if (addressType == 0x03 && !listen.namesGranted_domain)
                            {
                                LogErrorForConnection($"The domain name type of address is denied by configuration", connection, b, available);
                                processResponseForRequest(bb, EC_Address_type_not_supported);
                                return;
                            }

                            if (addressType == 0x04 && !listen.namesGranted_ipv6)
                            {
                                LogErrorForConnection($"ipv6 address is denied by configuration", connection, b, available);
                                processResponseForRequest(bb, EC_Address_type_not_supported);
                                return;
                            }

                            var ConnectToPort = b[available - 1] + (b[available - 2] << 8);

                            var fw = listen.forwarding;
                            try
                            {
                                // Смотрим, какой именно адрес и готовим для него почву
                                bool connected = false;
                                int networkUnreachable = 0;
                                int connectionRefused = 0;
                                int anotherError = 0;
                                if (addressType == 0x01 || addressType == 0x04) // Это IP-адреса
                                {
                                    bb.addWithCopy(b, -1, 4, available - 2);
                                    var addr = new IPAddress(bb.getBytes());

                                    try
                                    {
                                        LogForConnection("Request for connection to " + addr + ":" + ConnectToPort, connection, 3);

                                        // Устанавливает connectionTo
                                        if (!ConnectByIP(addr, ConnectToPort, fw, null, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError))
                                        {
                                            return;
                                        }

                                        connected = true;
                                    }
                                    catch (SocketException e)
                                    {
                                        LogForConnection("Error with try " + addr + ":" + ConnectToPort + "\r\n" + e.Message, connection, 2);
                                        return;
                                    }
                                }
                                else // Это доменные имена
                                {
                                    // b[4] - это размер доменного имени
                                    if (7 + b[4] != available)
                                    {
                                        processInvalidProtocolMessage();
                                        return;
                                    }

                                    bb.addWithCopy(b, -1, 5, 5 + b[4]);
                                    var domainName = asciiEncoding.GetString(bb.getBytes());
                                    connectToSocks = "(" + domainName + ")\t" + connectToSocks;

                                    // Проверяем, что здесь не передан IPv4 адрес вместо домена
                                    if (!listen.namesGranted_ipv4 && isIPv4(domainName))
                                    {
                                        LogErrorForConnection($"ipv4 addresses ({domainName}) is denied by configuration", connection, b, available);
                                        processResponseForRequest(bb, EC_Address_type_not_supported);
                                        return;
                                    }

                                    // Проверяем, что здесь не передан IPv6 адрес вместо домена
                                    if (!listen.namesGranted_ipv6 && isIPv6(domainName))
                                    {
                                        LogErrorForConnection($"ipv6 address is denied by configuration", connection, b, available);
                                        processResponseForRequest(bb, EC_Address_type_not_supported);
                                        return;
                                    }

                                    LogForConnection("Request for connection to '" + domainName + "'", connection, 3);

                                    listen.MaxSpeedTo = 0;
                                    if (listen.trusts_domain != null)
                                    {
                                        if (!listen.trusts_domain.Compliance(domainName, ref fw, ref listen.MaxSpeedTo))
                                        {
                                            LogForConnection($"Domain '{domainName}' is denied", connection, 1);
                                            processResponseForRequest(bb, EC_Denied);
                                            return;
                                        }
                                        
                                        LogForConnection($"SleepInterval for domain '{domainName}' is {listen.MaxSpeedTo}", connection, 5);
                                    }

                                    GetSocketForTarget(connection, ConnectToPort, fw, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError, domainName);
                                }

                                bb.Clear();

                                if (!connected)
                                {
                                    if (connectionRefused > 0)
                                        processResponseForRequest(bb, EC_Connection_refused);
                                    else
                                    if (anotherError > 0)
                                        processResponseForRequest(bb, EC_Host_unreachable);
                                    else
                                    if (networkUnreachable > 0)
                                        processResponseForRequest(bb, EC_Network_unreachable);
                                    else
                                        processResponseForRequest(bb, EC_general_SOCKS_server_failure);

                                    return;
                                }

                                // Здесь мы используем исключительно адрес, нам переданный
                                // Хотя по стандарту должны передавать адрес, с которым установлено соединение
                                processResponseForRequest(bb, EC_success);

                                doProcessTraffic();
                                successNegotiation = true;
                            }
                            catch (SocketException e)
                            {
                                if (e.ErrorCode == 11001)
                                {
                                    LogErrorForConnection($"Could not resolve host", connection, null, 0);
                                    processResponseForRequest(bb, EC_Network_unreachable);
                                }
                                else
                                {
                                    LogErrorForConnection($"Exception " + e.Message + "\r\n\r\n" + e.StackTrace, connection, b, available);
                                    processResponseForRequest(bb, EC_general_SOCKS_server_failure);
                                }
                            }
                            catch (Exception e)
                            {
                                LogErrorForConnection($"Exception " + e.Message + "\r\n\r\n" + e.StackTrace, connection, b, available);
                                processResponseForRequest(bb, EC_general_SOCKS_server_failure);
                            }

                        }
                        catch (Exception e)
                        {
                            LogErrorForConnection($"Exception " + e.Message + "\r\n\r\n" + e.StackTrace, connection, null, 0);
                        }
                        finally
                        {
                            if (!successNegotiation)
                            {
                                this.Dispose();
                            }
                        }
	                }
                );
            }

            private static string ResetMessageBody(string httpStr)
            {
                if (httpStr.Contains("\r\n\r\n"))
                    httpStr = httpStr.Substring(0, httpStr.IndexOf("\r\n\r\n") + 4);

                return httpStr;
            }

            public void GetSocketForTarget(Socket connection, int ConnectToPort, ForwardingInfo fi, ref bool connected, ref int networkUnreachable, ref int connectionRefused, ref int anotherError, string domainName)
            {
                IPAddress[] addresses = null;
                // Если нет перенаправления на другой прокси, то сами разрешаем доменное имя
                if (fi == null)
                {
                    addresses = Dns.GetHostAddresses(domainName);

                    // Перебираем возможные адреса соединения, если с одним не удалось соединить
                    foreach (var addr in addresses)
                    {
                        LogForConnection("Try connection to " + addr + ":" + ConnectToPort, connection, 4);
                        try
                        {
                            if (!ConnectByIP(addr, ConnectToPort, fi, null, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError))
                                continue;
    
                            connectToSocks += "\t" + connectionTo.LocalEndPoint + " -> " + connectionTo.RemoteEndPoint + "";
                            break;
                        }
                        catch (SocketException e)
                        {
                            if (e.ErrorCode == 10061)
                                connectionRefused++;
                            else
                            if (e.ErrorCode == 10051)
                                networkUnreachable++;
                            else
                                anotherError++;
    
                            LogForConnection("Error with try " + addr + ":" + ConnectToPort + "\r\n" + e.Message, connection, 3);
                        }
                    }
                }
                // Если есть перенаправление на другой прокси, то передаём ему доменное имя нетронутым
                else
                {
                    if (!ConnectByIP(null, ConnectToPort, fi, domainName, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError))
                    {
                        anotherError++;
                        return;
                    }
                }
            }

            public static int ReceiveBytes(Socket connection, byte[] b)
            {
                var offset = 0;
                var received = 0;
                do
                {
                    received = connection.Receive(b, offset, b.Length - offset, SocketFlags.None);
                    offset += received;
                }
                while (connection.Available > 0 && received > 0);

                return offset;
            }

            public void processInvalidProtocolMessage()
            {
                listen.Log($"error for connection {connection.RemoteEndPoint.ToString()}\r\nInvalid protocol. Socks5 protocol must be setted (or 'http'). Check protocol record in the client", 1);

                // Dispose уже вызывается в finally-блоке вызывающей функции
                // this.Dispose();
            }

            public void processResponseForRequest(BytesBuilder bb, byte replyCode)
            {
                bb.addByte(0x05);           // Версия протокола
                bb.addByte(replyCode);      // Код ошибки
                /*
                    REP    Reply field:
                     o  X'00' succeeded
                     o  X'01' general SOCKS server failure
                     o  X'02' connection not allowed by ruleset
                     o  X'03' Network unreachable
                     o  X'04' Host unreachable
                     o  X'05' Connection refused
                     o  X'06' TTL expired
                     o  X'07' Command not supported
                     o  X'08' Address type not supported
                     o  X'09' to X'FF' unassigned
                 */
                bb.addByte(0x00);
                // Если сообщаем об ошибке
                if (replyCode != 0)
                {
                    bb.addByte(0x01);   // ATYP = ipv4

                    bb.addByte(0x00);   // IP-адрес
                    bb.addByte(0x00);
                    bb.addByte(0x00);
                    bb.addByte(0x00);

                    bb.addByte(0x00);   // Порт
                    bb.addByte(0x00);
                }
                else
                if (connection.LocalEndPoint.AddressFamily == AddressFamily.InterNetwork)
                {
                    bb.addByte(0x01);   // ATYP = ipv4
                    var ipe = connectionTo.LocalEndPoint as IPEndPoint;
                    bb.add(ipe.Address.GetAddressBytes());
                    bb.addUshort((ushort) ipe.Port);
                }
                else
                {
                    bb.addByte(0x04);   // ATYP = ipv6
                    var ipe = connectionTo.LocalEndPoint as IPEndPoint;
                    bb.add(ipe.Address.GetAddressBytes());
                    //bb.addUshort((ushort) ipe.Port);
                    bb.addByte((byte) (ipe.Port >> 8));
                    bb.addByte((byte) ipe.Port);
                }

                var response = bb.getBytes();
                connection?.Send(response, 0, response.Length, SocketFlags.None);
            }
            
            public void Pulse()
            {
                lock (connection)
                    Monitor.PulseAll(connection);

                lock (connectionTo)
                    Monitor.PulseAll(connectionTo);
            }

            public void LogErrorForConnection(string Message, Socket connection, byte[] b, int available)
            {
                if (listen.debug > 4 && b != null)
                    listen.Log($"error for connection {connectToSocks}" + "\r\n" + Message + "\r\n\r\n" + BitConverter.ToString(b, 0, available > 1024 ? 1024 : available), 1, trusts.ErrorReporting.LogTypeCode.Error);
                else
                if (listen.debug > 0)
                    listen.Log($"error for connection {connectToSocks}" + "\r\n" + Message, 1, trusts.ErrorReporting.LogTypeCode.Error);
            }

            public void LogForConnection(string Message, Socket connection, int debugLevel)
            {
                listen.Log($"{connectToSocks}" + "\r\n" + Message, debugLevel);
            }

            public void LogDataForConnection(byte[] Message, int count, Socket connection, int debugLevel)
            {
                var str = "";
                if (count <= 4096)
                    str = Encoding.ASCII.GetString(Message, 0, count);
                else
                    str = Encoding.ASCII.GetString(Message, 0, 4096);

                listen.Log($"{connectToSocks}" + "\r\n[[[start data]]]\r\n" + str + "\r\n[[[end data]]]\r\n", debugLevel);
            }
        }

        public void newConnection(Socket connection)
        {
            lock (connections)
                this.connections.Add(new Connection(connection, this));

            Log("new connection from " + connection.RemoteEndPoint.ToString() + "\r\nCount of connections with the listener: " + this.connections.Count, 4);
        }
    }
}
