﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static vinnysocks5proxy.Helper;
using static trusts.Helper;
using trusts;

using System.Security.Cryptography;
using System.Text;

namespace vinnysocks5proxy
{
    public partial class ListenConfiguration: IDisposable, IComparable<ListenConfiguration>
    {
        public class UserPassword
        {
            public readonly string password;
            public readonly bool   isPasswordHash;

            public UserPassword(string password, bool isPasswordHash = false)
            {
                this.password       = password;
                this.isPasswordHash = isPasswordHash;

                if (this.isPasswordHash)
                    this.password = this.password.ToLowerInvariant();
            }

            public bool isWellPassword(string passwordFromInput)
            {
                if (!isPasswordHash)
                {
                    return SecureCompare(password, passwordFromInput);
                }

                var toHash = Encoding.ASCII.GetBytes(passwordFromInput);
                using (var sha = SHA512.Create())
                {
                    var hash       = sha.ComputeHash(toHash);
                    var hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    return SecureCompare(password, hashString);
                }
            }
        }

        public int        max_connections  = 64;
        public Socket     listen_socket    = null;
        public IPAddress  listen_ip        = default;
        public int        port             = 0;
        public IPEndPoint ipe              = default;
        public TrustsFile trusts_domain    = null;
        public Int64      MaxSpeedTo    = 0;

        public ForwardingInfo forwarding = null;

        public SortedList<string, UserPassword> users   = new SortedList<string, UserPassword>();
        public ErrorReporting_SimpleFile  logger  = new ErrorReporting_SimpleFile();

        public bool namesGranted_ipv4   = false;
        public bool namesGranted_ipv6   = false;
        public bool namesGranted_domain = false;
        public int  debug               = 0;        // Это для того, чтобы в лог выдавать чуть больше информации
        public bool Incorrect           = false;    // Если true, значит больше не будет прослушиваться

        public int TimeoutSendToTarget      = -1;
        public int TimeoutSendToClient      = -1;
        public int TimeoutReceiveFromClient = -1;
        public int TimeoutReceiveFromTarget = -1;

        public string ListenAddressForLog = "";

        // Всегда использовать lock(connections) при доступе. Это список соединений клиентов с сервером
        public List<Connection> connections   = new List<Connection>(128);

        public ListenConfiguration()
        {
            
        }
        
        public bool SetPort(int port)
        {
            this.port = port;

            return checkCorrect();
        }

        public bool checkCorrect()
        {
            if (port == 0)
                return false;

            ipe = new IPEndPoint(listen_ip, port);
            ListenAddressForLog   = ipe.ToString();
            logger.Identification = ListenAddressForLog;

            return true;
        }

        public void SetAddress(string AddressWithoutPort)
        {
            listen_ip = IPAddress.Parse(AddressWithoutPort);
        }

        public void Log(string Message, int debugLevel, ErrorReporting.LogTypeCode et = ErrorReporting.LogTypeCode.Usually)
        {
            if (debug > 0 && debug >= debugLevel)
            {
                logger.Log(Message, "", et);
            }
        }

        public void Listen(bool doLog = true)
        {
            try
            {
                if (doLog)
                Log($"Listening starting...", 4, ErrorReporting.LogTypeCode.Usually);
                listen_socket?.Dispose();
                listen_socket = null;

                listen_socket = new Socket(ipe.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                listen_socket.Bind(ipe);
                listen_socket.Listen(max_connections);

                Log($"Listening started", 3);
            }
            catch (Exception e)
            {
                try
                {
                    if (listen_socket != null)
                       listen_socket.Dispose();
                }
                catch (Exception)
                { }

                listen_socket = null;

                if (doLog)
                Log("Error occured in a start of listening\r\n" + e.Message, 0);
            }
        }

        public Task<Socket> Accept()
        {
            if (listen_socket == null || !listen_socket.IsBound)
            {
                Listen(false);

                Incorrect = true;
                return null;
            }

            return listen_socket.AcceptAsync();
        }

        public void Dispose()
        {
            lock (connections)
            {
                foreach (var connection in connections)
                {
                    try
                    {
                        if (connection == null)
                            continue;

                        connection.doTerminate = true;
                    }
                    catch (Exception e)
                    {
                        Log("Exception occured by the preparation to close connections process\r\n" + e.Message, 0);
                    }
                }
            }

            // Завершаем все соединения
            lock (connections)
            {
                foreach (var connection in connections)
                try
                {
                    connection.Dispose(doNotDelete: true);
                }
                catch (Exception e)
                {
                    Log("Exception occured by the close connections process\r\n" + e.Message, 0);
                }

                connections.Clear();
            }

            listen_socket?.Dispose();
            listen_socket = null;

            Log($"Listening ended", 0, ErrorReporting.LogTypeCode.Changed);
        }

        public int CompareTo(ListenConfiguration other)
        {
            if (this.port != other.port)
                return this.port - other.port;

            var a = this .listen_ip.GetAddressBytes();
            var b = other.listen_ip.GetAddressBytes();

            if (a.Length != b.Length)
                return a.Length - b.Length;
                
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return a[i] - b[i];
            }

            return 0;
        }
    }
}
