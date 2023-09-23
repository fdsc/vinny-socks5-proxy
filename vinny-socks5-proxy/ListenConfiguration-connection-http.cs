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

namespace vinnysocks5proxy
{
    public partial class ListenConfiguration: IDisposable, IComparable<ListenConfiguration>
    {
        public partial class Connection: IDisposable
        {
            // Чтобы FireFox постоянно не спрашивал пароль от прокси, нужно изменить настройку signon.autologin.proxy на true (about:config)

            // https://datatracker.ietf.org/doc/html/rfc7230#page-19
            // Без Content-Length:0 клиент чего-то ждёт. Вероятнее всего, в связи с тем,
            // что в "3.3.  Message Body" указаны только несколько статусов ответоч, которые не имеют "message-body"

            /// <summary>Посылает HTTP-ответ без тела. Это аварийная функция под catch - не генерирует исключений</summary>
            /// <param name="Response">Http-ответ, без строки HTTP/1.1 в начале и переводов строки в конце</param>
            /// <param name="connection">Сокет, на который отсылается ответ</param>
            public void SendHttpResponse(string Response, Socket connection)
            {
                try
                {
                    var responseBytes = asciiEncoding.GetBytes("HTTP/1.1 " + Response + "\r\nContent-Length:0\r\n\r\n");
                    connection.Send(responseBytes);
                }
                catch
                {}
            }

            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods/CONNECT
            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Proxy-Authorization
            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/407
            // Сюда поступает соединение, которое началось как HTTP CONNECT
            public bool doHttpConnect(Socket connection, ListenConfiguration listen, string HttpHello)
            {
                try
                {
                    // Принимаем на вход CONNECT domain:port HTTP/1.1
                    // Разделяем на заголовки
                    var headers = HttpHello.Split(new string[] {"\r\n"}, StringSplitOptions.None);
                    
                    var connectString = headers[0];
                    var connect       = connectString.Split(new string[] {" "}, StringSplitOptions.RemoveEmptyEntries);

                    // В первой строке должно быть три разделённых пробелами лексемы
                    if (connect.Length != 3 || connect[0].ToLowerInvariant() != "connect" || (connect[2].ToLowerInvariant() != "http/1.1" && connect[2].ToLowerInvariant() != "http/1.0"))
                    {
                        LogForConnection($"incorrect http connection\r\n{HttpHello}", connection, 0);
                        SendHttpResponse("400 Bad Request", connection);
                        return false;
                    }

                    // Вычисляем адрес как соединение имени домена и порта подключения
                    var address = connect[1].Split(new string[] {":"}, StringSplitOptions.None);
                    if (address.Length != 2)
                    {
                        LogForConnection($"incorrect http connection (must have 'domain:port')\r\n{HttpHello}", connection, 0);
                        SendHttpResponse("400 Bad Request", connection);
                        return false;
                    }

                    var port   = Int32.Parse(address[1]);
                    var domain = address[0];

                    connectToSocks = "(" + domain + ")\t" + connectToSocks;
                    LogForConnection("Request for connection to '" + domain + "'", connection, 3);

                    var fw = listen.forwarding;
                    // Проверяем, разрешён ли данный домен
                    listen.MaxSpeedTo = 0;
                    if (listen.trusts_domain != null)
                    {
                        if (!listen.trusts_domain.Compliance(domain, ref fw, ref listen.MaxSpeedTo))
                        {
                            LogForConnection($"Domain '{domain}' is denied\r\n{HttpHello}", connection, 1);
                            SendHttpResponse("403 Forbidden by proxy", connection);
                            return false;
                        }
                        
                        LogForConnection($"SleepInterval for domain '{domain}' is {listen.MaxSpeedTo}", connection, 5);
                    }

                    // Требуется аутентификация
                    if (listen.users.Count > 0)
                    {
                        var      AuthString   = "";
                        var      second       = "";
                        string[] UserPassword = null;
                        int      count        = 0;
                        do
                        {
                            foreach (var header in headers) // Ниже копия
                            {
                                if (header.StartsWith("Proxy-Authorization:"))
                                {
                                    AuthString = header.Substring(startIndex: "Proxy-Authorization:".Length).Trim();
                                    if (AuthString.ToLowerInvariant().StartsWith("basic"))
                                    {
                                        AuthString = AuthString.Substring(startIndex: "basic".Length).Trim();
                                        goto ProxyAuthorization;
                                    }
                                }
                            }

                            SendHttpResponse($"407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"For http-proxy {connection.LocalEndPoint}\"", connection);
                            var received = ReceiveBytes(connection, BytesTo);
                            second       = asciiEncoding.GetString(BytesTo, 0, received);
                            headers      = second.Split(new string[] {"\r\n"}, StringSplitOptions.None);

                            count++;
                        }
                        while (count < 2);

                        ErrorAuth:

                            LogForConnection($"Incorrect authorization: {second}\r\n", connection, 0);
                            SendHttpResponse("400 Bad Request", connection);
                            return false;

                        Forbidden:
                            LogForConnection($"No have user {UserPassword[0]}", connection, 1);
                            SendHttpResponse("403 Forbidden", connection);
                            return false;

                        ProxyAuthorization:
                        
                        AuthString   = asciiEncoding.GetString(  System.Convert.FromBase64String(AuthString)  );
                        UserPassword = AuthString.Split(new string[] { ":" }, StringSplitOptions.None);
                        if (UserPassword.Length != 2)
                            goto ErrorAuth;

                        var key = listen.users.IndexOfKey(UserPassword[0]);
                        if (key < 0)
                        goto Forbidden;
                        
                        if (!listen.users[UserPassword[0]].isWellPassword(UserPassword[1]))
                            goto Forbidden;
                    }
                    
                    // Прошли аутентификацию либо она не требуется.
                    // Пробуем соединиться с целевым сервером
                    // var addresses = Dns.GetHostAddresses(domain);

                    bool connected = false;
                    int networkUnreachable = 0, connectionRefused = 0, anotherError = 0;
                    // Перебираем возможные адреса соединения, если с одним не удалось соединить
                    GetSocketForTarget(connection, port, fw, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError, domain);

                    // Если произошла ошибка
                    if (!connected)
                    {
                        if (connectionRefused > 0)
                            SendHttpResponse("499 Client Closed Request", connection);
                        else
                        if (anotherError > 0)
                            SendHttpResponse("520 Unknown Error", connection);
                        else
                        if (networkUnreachable > 0)
                            SendHttpResponse("523 Origin Is Unreachable", connection);
                        else
                            SendHttpResponse("520 Unknown Error", connection);

                        return false;
                    }

                    // Подсоединились. Можем посылать ответ, что всё хорошо
                    // https://datatracker.ietf.org/doc/html/rfc7231#section-4.3.6
                    // Не заменять на SendHttpResponse, т.к. там передаётся Content-Length, который нельзя передавать здесь
                    var responseBytes = asciiEncoding.GetBytes("HTTP/1.1 200 Successful\r\n\r\n");
                    connection.Send(responseBytes);

                    doProcessTraffic();

                    return true;
                }
                catch (Exception e)
                {
                    LogForConnection($"error in http connection\r\n{HttpHello}\r\n" + e.Message + "\r\n" + e.StackTrace, connection, 0);
                    SendHttpResponse("400 Bad Request", connection);
                    return false;
                }
            }
            
            /// <summary>Это web-прокси без туннелирования (без CONNECT)</summary>
            /// <returns><c>true</c>, запрос успешно перенаправлен, <c>false</c> произошла ошибка</returns>
            /// <param name="connection">Сокет подключения с клиентом</param>
            /// <param name="listen">Объект прослушивателя</param>
            /// <param name="HttpHello">Запрос от клиента, переведённый в ASCII-кодировку</param>
            /// <param name="b">Запрос от клиента</param>
            /// <param name="b_length">Общая длина запроса от клиента</param>
            public bool doHttpWithoutConnect(Socket connection, ListenConfiguration listen, string HttpHello, byte[] b, int b_length)
            {
                start:

                // Т.к. каждое соединение тут для разных целевых серверов, то всю информацию заново перезагружаем
                connectToSocks = $"{connection.LocalEndPoint.ToString()} <- {connection.RemoteEndPoint.ToString()}";
                try
                {
                // Это будет выдавать ошибку, если включён HTTP pipelining
                /*
                    if (connection.Available > 0)
                    {
                        LogForConnection($"doHttpWithoutConnect: error: connection.Available > 0 ({connection.Available}) for\r\n" + HttpHello, connection, 0);
                        SendHttpResponse("500 Internal Server Error", connection);
                        return false;
                    }*/

                    // Принимаем на вход GET или другой запрос, например, GET / HTTP/1.1
                    // Разделяем на заголовки
                    var headers = HttpHello.Split(new string[] {"\r\n"}, StringSplitOptions.None);

                    int received;
                    // Требуется аутентификация
                    if (listen.users.Count > 0)
                    {
                        var      AuthString   = "";
                        string[] UserPassword = null;
                        int      count        = 0;

                        do
                        {
                            foreach (var header in headers) // Ниже копия
                            {
                                if (header.StartsWith("Proxy-Authorization:"))
                                {
                                    AuthString = header.Substring(startIndex: "Proxy-Authorization:".Length).Trim();
                                    if (AuthString.ToLowerInvariant().StartsWith("basic"))
                                    {
                                        AuthString = AuthString.Substring(startIndex: "basic".Length).Trim();
                                        goto ProxyAuthorization;
                                    }
                                }
                            }
    
                            SendHttpResponse($"407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"For http-proxy {connection.LocalEndPoint}\"", connection);
                            received  = ReceiveBytes(connection, b);
                            b_length  = received;
                            HttpHello = asciiEncoding.GetString(BytesTo, 0, received);
                            HttpHello = ResetMessageBody(HttpHello);
                            headers   = HttpHello.Split(new string[] {"\r\n"}, StringSplitOptions.None);

                            count++;
                        }
                        while (count < 2);

                        ErrorAuth:

                            LogForConnection($"Incorrect authorization: {HttpHello}\r\n", connection, 0);
                            SendHttpResponse("400 Bad Request", connection);
                            return false;

                        Forbidden:
                            LogForConnection($"No have user {UserPassword[0]}", connection, 1);
                            SendHttpResponse("403 Forbidden", connection);
                            return false;

                        ProxyAuthorization:

                        AuthString   = asciiEncoding.GetString(  System.Convert.FromBase64String(AuthString)  );
                        UserPassword = AuthString.Split(new string[] { ":" }, StringSplitOptions.None);
                        if (UserPassword.Length != 2)
                            goto ErrorAuth;

                        var key = listen.users.IndexOfKey(UserPassword[0]);
                        if (key < 0)
                            goto Forbidden;

                        if (!listen.users[UserPassword[0]].isWellPassword(UserPassword[1]))
                            goto Forbidden;
                    }

                    // Прошли аутентификацию либо она не требуется.
                    var connectString = headers[0];
                    var connect       = connectString.Split(new string[] {" "}, StringSplitOptions.RemoveEmptyEntries);

                    // В первой строке должно быть три разделённых пробелами лексемы
                    // Пример
                    // POST http://ocsp2.globalsign.com/gsorganizationvalsha2g2 HTTP/1.1
                    if (connect.Length != 3 || (connect[2].ToLowerInvariant() != "http/1.1" && connect[2].ToLowerInvariant() != "http/1.0"))
                    {
                        LogForConnection($"incorrect http web-connection\r\n{HttpHello}", connection, 0);
                        SendHttpResponse("400 Bad Request", connection);
                        return false;
                    }

                    // host сейчас - это полный url запроса
                    var host = connect[1];
                    // Обрабатываем только http
                    if (/*!host.StartsWith("https://") && */!host.ToLowerInvariant().StartsWith("http://"))
                    {
                        LogForConnection($"incorrect http web-connection\r\n{HttpHello}", connection, 0);
                        SendHttpResponse("400 Bad Request", connection);
                        return false;
                    }

                    host = host.Substring("http://".Length);

                    // Пытаемся отделить host от, собственно, пути запроса
                    var index = host.IndexOf("/");
                    if (index < 0)
                    {
                        LogForConnection($"incorrect http web-connection\r\n{HttpHello}", connection, 0);
                        SendHttpResponse("400 Bad Request", connection);
                        return false;
                    }

                    // После выполнения следующих инструкций
                    // httpPath - это /gsorganizationvalsha2g2
                    // host - это ocsp2.globalsign.com
                    var httpPath = host.Substring(startIndex: index);
                    host = host.Substring(startIndex: 0, length: index);

                    // Вычисляем адрес как соединение имени домена и порта подключения
                    // @ - это значок базовой аутентификации. Мы его не позволяем
                    var address = host.Split(new string[] {":"}, StringSplitOptions.None);
                    if (address.Length > 2 || host.Contains("@"))
                    {
                        LogForConnection($"incorrect http web-connection\r\n{HttpHello}", connection, 0);
                        SendHttpResponse("400 Bad Request", connection);
                        return false;
                    }

                    // Так как мы осуществляем соединения только по http, то порт может быть только один, если явно не указано иное
                    var port = 80;
                    if (address.Length > 1)
                        port   = Int32.Parse(address[1]);

                    var domain = address[0];

                    connectToSocks = "(" + domain + ")\t" + connectToSocks;
                    LogForConnection("web-request for connection to '" + domain + "'", connection, 3);

                    var fw = listen.forwarding;
                    // Проверяем, разрешён ли данный домен
                    listen.MaxSpeedTo = 0;
                    if (listen.trusts_domain != null)
                    {
                        if (!listen.trusts_domain.Compliance(domain, ref fw, ref listen.MaxSpeedTo))
                        {
                            LogForConnection($"Domain '{domain}' is denied\r\n{HttpHello}", connection, 1);
                            SendHttpResponse("403 Forbidden by proxy", connection);
                            return false;
                        }
                        
                        LogForConnection($"SleepInterval for domain '{domain}' is {listen.MaxSpeedTo}", connection, 5);
                    }

                    // Пробуем соединиться с целевым сервером
                    // var addresses = Dns.GetHostAddresses(domain);

                    bool connected = false;
                    int networkUnreachable = 0, connectionRefused = 0, anotherError = 0;
                    // Перебираем возможные адреса соединения, если с одним не удалось соединить
                    GetSocketForTarget(connection, port, fw, ref connected, ref networkUnreachable, ref connectionRefused, ref anotherError, domain);

                    // Если произошла ошибка
                    if (!connected)
                    {
                        if (connectionRefused > 0)
                            SendHttpResponse("499 Client Closed Request", connection);
                        else
                        if (anotherError > 0)
                            SendHttpResponse("520 Unknown Error", connection);
                        else
                        if (networkUnreachable > 0)
                            SendHttpResponse("523 Origin Is Unreachable", connection);
                        else
                            SendHttpResponse("520 Unknown Error", connection);

                        return false;
                    }

                    // Подсоединились
                    // Теперь нужно сформировать новый запрос
                    var sb = new StringBuilder(b_length);
                    sb.Append(connect[0] + " " + httpPath + " " + connect[2] + "\r\n");

                    var  content_length = 0;
                    bool first          = true;
                    foreach (var header in headers)
                    {
                        // Начальный заголовок мы также формируем сами, так что его пропускаем
                        if (first)
                        {
                            first = false;
                            continue;
                        }
                        // Это пустая строка - символ конца заголовков
                        if (header.Length == 0)
                            break;

                        // Не включаем заголовок с авторизационной информацией на сервер
                        if (header.ToLowerInvariant().StartsWith("proxy-authorization:"))
                            continue;

                        if (header.ToLowerInvariant().StartsWith("content-length:"))
                        {
                            // Если content_length один раз уже был установлен, то запрос неверен. Отказываемся его обрабатывать
                            if (content_length > 0)
                            {
                                LogForConnection($"incorrect http web-connection\r\n{HttpHello}", connection, 0);
                                SendHttpResponse("400 Bad Request", connection);
                                return false;
                            }

                            content_length = int.Parse(  header.Substring("content_length:".Length).Trim()  );
                        }

                        sb.Append(header + "\r\n");
                    }
                    // Заголовки закончились
                    sb.Append("\r\n");

                    var responseBytes = asciiEncoding.GetBytes(sb.ToString());
                    var bb = new BytesBuilder();
                    bb.add(responseBytes);

                    var content_offset = 0;
                    // Ищем в массиве b начало контента, отсылаемого на сервер
                    if (content_length > 0)
                    for (int i = 4; i < b_length; i++)
                    {
                        // Конец заголовков у нас 
                        if (b[i-0] != '\n')
                            continue;
                        if (b[i-1] != '\r')
                            continue;
                        if (b[i-2] != '\n')
                            continue;
                        if (b[i-3] != '\r')
                            continue;

                        content_offset = i + 1;
                    }

                    if (content_offset > 0)
                    {
                        if (b_length < content_offset + content_length)
                        {
                            LogForConnection($"incorrect http web-connection (b_length < content_offset + content_length)\r\n{HttpHello}", connection, 0);
                            SendHttpResponse("400 Bad Request", connection);
                            return false;
                        }

                        bb.addWithCopy(b, -1, content_offset, content_offset + content_length);
                    }


                    // Принимаем всё асинхронно от целевого сервера. От клиента больше ничего не принимаем и не передаём
                    // Важно то, что при каждом новом запросе у нас новое соединение, так что мы заново устанавливаем приём
                    setAcyncReceiveFrom();  // Этот запрос принимает ответ от нижестоящего соединения

                    var sended = connectionTo.Send(bb.getBytes(), 0, (int) bb.Count, SocketFlags.None);
                    SizeOfTransferredDataTo += bb.Count;
                    //bb.Clear();


                    // Принимаем следующие запросы от клиента к другим целевым адресам
                    // Почему-то без этого браузер не работает: закрытие соединения он не понимает верно
                    try
                    {
                        // Здесь может быть ошибка при http pipeline, т.к. запрос принимается только один, а клиент реально может передать сразу несколько
                        received  = ReceiveBytes(connection, b);
                        b_length  = received;
                        HttpHello = asciiEncoding.GetString(b, 0, received);
                        HttpHello = ResetMessageBody(HttpHello);

                        if (received > 0)
                        {
                            oldConnectionTo = connectionTo;
                            try { connectionTo.Dispose(); } catch { }
                            connectionTo = null;

                            // Немного ждём, чтобы удалить старое соединение
                            Thread.Sleep(100);

                            goto start;
                        }
                        else
                            return true;
                    }
                    catch
                    {
                        Dispose();
                        return true;
                    }
                }
                catch (Exception e)
                {
                    LogForConnection($"error in http web-connection\r\n{HttpHello}\r\n" + e.Message + "\r\n" + e.StackTrace, connection, 0);
                    SendHttpResponse("400 Bad Request", connection);
                    return false;
                }
            }

            protected Socket oldConnectionTo = null;
        }
    }
}
