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
            public void setAcyncReceiveTo()
            {
                if (doTerminate)
                {
                    Dispose();
                    return;
                }

                try
                {
                    var sa = new SocketAsyncEventArgs();
                    sa.Completed += ReceiveAsyncTo;
                    sa.SetBuffer(BytesTo, 0, BytesTo.Length);

                    if (!connection.ReceiveAsync(sa))
                        ReceiveAsyncTo(this, sa);
                }
                catch (SocketException e)
                {
                    if (!doTerminate)
                        LogForConnection(e.Message, connection, 3);

                    Dispose();
                    return;
                }
                catch (Exception e)
                {
                    if (!doTerminate)
                        LogForConnection(e.Message + "\r\n" + e.StackTrace, connection, 2);

                    Dispose();
                    return;
                }
            }

            public void ReceiveAsyncTo(object sender, SocketAsyncEventArgs e)
            {
                if (doTerminate)
                {
                    Dispose();
                    return;
                }

                try
                {
                    if (e.SocketError != SocketError.Success)
                        LogForConnection("Socket error " + e.SocketError, connection, 3);

                    if (e.BytesTransferred == 0)
                    {
                        LogForConnection("The socket did not transmit any data (from client) and will be shutdown", e.ConnectSocket, 4);
                        Dispose();

                        return;
                    }

                    SetLastActiveConnectionTimerCounter();

                    int sended = 0;

                    LogDataForConnection(e.Buffer, e.BytesTransferred, e.ConnectSocket, 7);

                    sended = connectionTo.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);
                    SpeedOfConnectionTo += sended;

                    setAcyncReceiveTo();
                    if (listen.debug > 4)
                    LogForConnection("Transfer data to, size " + sended, connection, 5);
                }
                catch (SocketException ex)
                {
                    if (!doTerminate)
                        LogForConnection(ex.Message, connection, 3);

                    Dispose();
                    return;
                }
                catch (Exception ex)
                {
                    if (!doTerminate)
                        LogForConnection(ex.Message + "\r\n" + ex.StackTrace, connection, 2);

                    Dispose();
                    return;
                }
            }

            public void setAcyncReceiveFrom()
            {
                if (doTerminate)
                {
                    Dispose();
                    return;
                }

                try
                {
                    var sa = new SocketAsyncEventArgs();
                    sa.Completed += ReceiveAsyncFrom;
                    sa.SetBuffer(BytesFrom, 0, BytesFrom.Length);

                    if (!connectionTo.ReceiveAsync(sa))
                        ReceiveAsyncFrom(this, sa);
                }
                catch (SocketException e)
                {
                    if (!doTerminate)
                        LogForConnection(e.Message, connection, 3);

                    // !isEstablished - это запросы от doHttpWithoutConnect
                    if (isEstablished)
                        Dispose();

                    return;
                }
                catch (Exception e)
                {
                    if (!doTerminate)
                        LogForConnection(e.Message + "\r\n" + e.StackTrace, connection, 2);

                    // !isEstablished - это запросы от doHttpWithoutConnect
                    if (isEstablished)
                        Dispose();

                    return;
                }
            }

            public void ReceiveAsyncFrom(object sender, SocketAsyncEventArgs e)
            {
                if (doTerminate)
                {
                    Dispose();
                    return;
                }

                try
                {
                    // Если соединение было завершено, ничего не делаем
                    if (connectionTo == null || e.ConnectSocket == oldConnectionTo)
                        return;

                    if (e.SocketError != SocketError.Success)
                        LogForConnection("Socket error " + e.SocketError, connection, 3);

                    if (e.BytesTransferred == 0)
                    {
                        LogForConnection("The socket did not transmit any data (from target server) and will be shutdown", e.ConnectSocket, 4);

                        if (isEstablished)
                            Dispose();

                        return;
                    }

                    SetLastActiveConnectionTimerCounter();

                    int sended = 0;
    
                    LogDataForConnection(e.Buffer, e.BytesTransferred, e.ConnectSocket, 6);

                    sended = connection.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);
                    SpeedOfConnectionFrom += sended;
    
                    setAcyncReceiveFrom();
                    if (listen.debug > 4)
                    LogForConnection("Transfer data from, size " + sended, connection, 5);
                }
                catch (SocketException ex)
                {
                    if (!doTerminate)
                        LogForConnection(ex.Message, connection, 3);

                    // !isEstablished - это запросы от doHttpWithoutConnect
                    if (isEstablished)
                        Dispose();

                    return;
                }
                catch (Exception ex)
                {
                    if (!doTerminate)
                        LogForConnection(ex.Message + "\r\n" + ex.StackTrace, connection, 2);

                    if (isEstablished)
                        Dispose();

                    return;
                }
            }

            public void doProcessTraffic()
            {
                isEstablished = true;
                LogForConnection($"Starting connections for user data for {connectionTo.LocalEndPoint} -> {connectionTo.RemoteEndPoint}", connection, 2);

                if (listen.TimeoutSendToClient > 0)
                connection  .SendTimeout    = listen.TimeoutSendToClient;
                
                if (listen.TimeoutSendToTarget > 0)
                connectionTo.SendTimeout    = listen.TimeoutSendToTarget;

                if (listen.TimeoutReceiveFromClient > 0)
                connection  .ReceiveTimeout = listen.TimeoutReceiveFromClient;
                
                if (listen.TimeoutReceiveFromTarget > 0)
                connectionTo.ReceiveTimeout = listen.TimeoutReceiveFromTarget;

                setAcyncReceiveTo();
                setAcyncReceiveFrom();
            }
        }
    }
}
