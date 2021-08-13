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
        public partial class Connection: IDisposable
        {
            // Работаем с подключением напрямую, без дополнительного прокси
            public bool ConnectByIPWithoutForwarding(IPAddress toIP, int ConnectToPort, ForwardingInfo fi, string requestDomain, ref bool connected, ref int networkUnreachable, ref int connectionRefused, ref int anotherError)
            {
                connectionTo = new Socket(toIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                var ipe = new IPEndPoint(toIP, ConnectToPort);
                connectToSocks += "\t(" + ipe + ")";

                try
                {
                    connectionTo.Connect(ipe);
                    connected = true;

                    return true;
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

                    LogForConnection("Error with try " + ipe + "\r\n" + e.Message, connection, 2);
                    return false;
                }
            }

            public bool ConnectByIP(IPAddress toIP, int ConnectToPort, ForwardingInfo fi, string requestDomain, ref bool connected, ref int networkUnreachable, ref int connectionRefused, ref int anotherError)
            {
                if (requestDomain == null && toIP == null)
                    throw new ArgumentNullException("ListenConfiguration.Connection.ConnectByIP: toIP and requestDomain is null");

                // Если нет перенаправления на другой прокси
                if (fi == null)
                {
                    return ConnectByIPWithoutForwarding(toIP, ConnectToPort, fi, null, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError);
                }
                // Если есть перенаправление на другой прокси
                else
                {
                    fi.parse();
                    connectionTo = new Socket(fi.address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    var ipe  = new IPEndPoint(fi.address, fi.forwardingPort);


                    connectToSocks += "\t(" + ipe + " [forwarding])";

                    LogForConnection($"Try to connect to forwarding proxy {ipe}", connection, 4);
                    connectionTo.Connect(ipe);

                    // https://datatracker.ietf.org/doc/html/rfc7230
                    // Посылаем запрос socks5-серверу на соединение без авторизации
                    connectionTo.Send(sendS5Forwarding1);

                    // Ждём в ответ два байта
                    var available = waitAvailableBytes(connectionTo, 2);

                    if (available == 0)
                    {
                        LogForConnection($"No available bytes with forwarding to {ipe}", connection, 0);
                        return false;
                    }

                    // Принимаем ответ: он должен быть ровно два байта: 05 и 00 - соединение без авторизации подтверждено
                    int offset = ReceiveBytes(connectionTo, BytesTo);
                    if (BytesTo[0] != 0x05 || BytesTo[1] != 0)
                    {
                        LogForConnection($"Incorrect first response by {ipe}", connection, 0);
                        return false;
                    }

                    var bb = new BytesBuilder();
                    // Посылаем для прокси адрес для соединения с целевым сервером
                    if (toIP != null)
                    {
                        bb.addByte(5);      // Версия протокола
                        bb.addByte(1);      // Команда CONNECT
                        bb.addByte(0);      // Зарезервированный байт
                        var aType = toIP.AddressFamily == AddressFamily.InterNetwork ? 1 : 4;   // Тип адреса

                        bb.addByte  (  (byte) aType  );
                        bb.add      (  toIP.GetAddressBytes()  );
                        bb.addUshort(  (ushort) ConnectToPort  );

                        connectionTo.Send(bb.getBytes());
                        bb.Clear();
                    }
                    else
                    {
                        bb.addByte(5);      // Версия протокола
                        bb.addByte(1);      // Команда CONNECT
                        bb.addByte(0);      // Зарезервированный байт
                        bb.addByte(3);      // Типа адреса: доменное имя

                        var dnBytes = asciiEncoding.GetBytes(requestDomain);
                        if (dnBytes.Length > 255)
                        {
                            LogForConnection($"Incorrect (very long) domain name: {requestDomain}", connection, 0);
                            return false;
                        }

                        bb.addByte  (   (byte) dnBytes.Length   );
                        bb.add      (dnBytes);
                        // bb.addUshort(  (ushort) ConnectToPort  );
                        // var ConnectToPort = b[available - 1] + (b[available - 2] << 8);
                        bb.addByte((byte) (ConnectToPort >> 8));
                        bb.addByte((byte) ConnectToPort);

                        connectionTo.Send(bb.getBytes());
                        // bb.Clear();
                    }

                    available = waitAvailableBytes(connectionTo, 7);
                    if (available == 0)
                    {
                        LogForConnection($"Not available bytes from [forwarding] proxy after connect request: {BytesTo[1]}", connection, 0);
                        return false;
                    }

                    offset = ReceiveBytes(connectionTo, BytesTo);

                    // Если соединение неудачно
                    if (BytesTo[0] != 0x05 && BytesTo[1] != 0)
                    {
                        LogForConnection($"Error from the [forwarding] proxy: {BytesTo[1]}", connection, 0);
                        return false;
                    }

                    // Тип адреса - IPv4
                    /*
                    if (BytesTo[3] == 1)
                    {
                        bb.Clear();
                        bb.addWithCopy(BytesTo, -1, 4, 4+4);
                        var boudedIP    = new IPAddress(bb.getBytes());
                        var boundedPort = BytesTo[available - 1] + (BytesTo[available - 2] << 8);
                        connectToSocks += $"\t{boudedIP}:{boundedPort}";    // TOR возвращает здесь одни нули
                    }
                    */

                    LogForConnection($"Connected to forwarding proxy {ipe}", connection, 3);
                    connected = true;

                    return true;
                }
            }

            protected byte[] sendS5Forwarding1 = new byte[] { 0x05, 1, 0 };
        }
    }
}
