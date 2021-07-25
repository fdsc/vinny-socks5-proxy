using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static vinnysocks5proxy.Helper;
using cryptoprime;
using System.Text;

namespace vinnysocks5proxy
{
    public partial class ListenConfiguration: IDisposable, IComparable<ListenConfiguration>
    {
        public partial class Connection: IDisposable
        {
            public void doProcessTraffic()
            {
                listen.LogForConnection("Starting connections for user data for " + connectionTo.RemoteEndPoint, connection);

                var clientData = new Thread
                (
                    delegate ()
                    {
                        var b = new byte[65536];
                        int available = 0;
                        int sended    = 0;

                        do
                        {
                            try
                            {
                                // Ждём данные от клиента
                                waitBytes(connection, b, ref available, listen.SleepTimeTo, listen.SleepTimeToBytes);

                                // Посылаем даные клиента на удалённый сервер
                                if (available > 0 && !doTerminate)
                                {
                                    sended = connectionTo.Send(b, available, SocketFlags.None);
                                    SizeOfTransferredDataTo += sended;

                                    if (listen.debug > 4)
                                    listen.LogForConnection("Transfer data to, size " + sended, connection);
                                }
                            }
                            catch (Exception e)
                            {
                                if (doTerminate)
                                    return;

                                try
                                {
                                    listen.LogForConnection("Error with client data for " + connectionTo.RemoteEndPoint + "\r\n" + e.Message, connection);
                                }
                                catch
                                {
                                    listen.LogForConnection("Error with client data for ???\r\n" + e.Message, connection);
                                }
                            }
                        }
                        while (!doTerminate);
	                }
                );
                clientData.IsBackground = true;
                clientData.Start();

                var serverData = new Thread
                (
                    delegate ()
                    {
                        var b = new byte[65536];
                        int available = 0;
                        int sended    = 0;

                        do
                        {
                            try
                            {
                                // Ждём данные от клиента
                                waitBytes(connectionTo, b, ref available, listen.SleepTimeFrom, listen.SleepTimeFromBytes);

                                // Посылаем даные клиента на удалённый сервер
                                if (available > 0 && !doTerminate)
                                {
                                    sended = connection.Send(b, available, SocketFlags.None);
                                    SizeOfTransferredDataFrom += sended;

                                    if (listen.debug > 4)
                                    listen.LogForConnection("Transfer data from, size " + sended, connection);
                                }
                            }
                            catch (Exception e)
                            {
                                if (doTerminate)
                                    return;
                                   
                                try
                                {
                                    listen.LogForConnection("Error with remote server data for " + connectionTo.RemoteEndPoint + "\r\n" + e.Message, connection);
                                }
                                catch
                                {
                                    listen.LogForConnection("Error with remote server data for ???\r\n" + e.Message, connection);
                                }
                            }
                        }
                        while (!doTerminate);

                        Dispose();
                    }
                );
                serverData.IsBackground = true;
                serverData.Start();
            }
            
            public void waitBytes(Socket connection, byte[] b, ref int available, int SleepTime, int SleepTimeBytes)
            {
                var offset   = 0;
                var recieved = 0;
                available = 0;
                do
                {
                    if (offset > 0 && listen.debug > 4)
                        listen.LogForConnection($"sleeped / bytes {offset}", connection);

                    recieved   = connection.Receive(b, offset, b.Length - offset, SocketFlags.None);
                    offset    += recieved;
                    available += recieved;

                    if (SleepTime >= 0 && connection.Available == 0 && offset < SleepTimeBytes)
                        Thread.Sleep(SleepTime);
                }
                while (connection.Available > 0 && offset < SleepTimeBytes);
            }
        }
    }
}
