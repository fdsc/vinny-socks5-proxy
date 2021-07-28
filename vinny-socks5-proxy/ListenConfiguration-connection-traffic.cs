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
                        LogForConnection("The socket did not transmit any data and will be shutdown", connection, 3);
                        Dispose();
                        return;
                    }
    
                    int sended = 0;
    
                    sended = connectionTo.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);
                    SizeOfTransferredDataTo += sended;
    
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
                    sa.SetBuffer(BytesTo, 0, BytesTo.Length);
    
                    if (!connectionTo.ReceiveAsync(sa))
                        ReceiveAsyncFrom(this, sa);
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

            public void ReceiveAsyncFrom(object sender, SocketAsyncEventArgs e)
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
    
                    int sended = 0;
    
                    sended = connection.Send(e.Buffer, e.BytesTransferred, SocketFlags.None);
                    SizeOfTransferredDataFrom += sended;
    
                    setAcyncReceiveFrom();
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

            public void doProcessTraffic()
            {
                LogForConnection($"Starting connections for user data for {connectionTo.LocalEndPoint} -> {connectionTo.RemoteEndPoint}", connection, 2);

                setAcyncReceiveTo();
                setAcyncReceiveFrom();
            }
        }
    }
}
