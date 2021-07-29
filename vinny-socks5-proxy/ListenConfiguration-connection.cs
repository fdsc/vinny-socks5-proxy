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
using System.Diagnostics;

namespace vinnysocks5proxy
{
    public partial class ListenConfiguration: IDisposable, IComparable<ListenConfiguration>
    {
        public partial class Connection: IDisposable
        {
            public Socket connection   = null;
            public Socket connectionTo = null;
            public long   SizeOfTransferredDataTo   = 0;
            public long   SizeOfTransferredDataFrom = 0;
            public readonly ListenConfiguration listen = null;
            
            public bool   isEstablished = false;

            /// <summary>Время последней активности по счётчику MainClass.TimeCounter</summary>
            public volatile int LastActiveConnectionTimerCounter = 0;

            public int waitAvailableBytes(int minBytes = 1, int timeout = 100, int countOfTimeouts = 100)
            {
                int available = 0;
                // lock (connection)
                {
                    available = connection?.Available ?? 0;
                    int count = 0;
                    while (available < minBytes)
                    {
                        try { lock (connection) Monitor.Wait(connection, timeout); } catch { }

                        if (connection == null || isDisposed || doTerminate)
                            return 0;

                        available = connection.Available;

                        count++;
                        if (count > countOfTimeouts)
                        {
                            listen.Log($"error for connection {connectToSocks}: not enought bytes; available {available}", 0);
                            // this.Dispose();
                            return 0;
                        }
                    }
                }

                SetLastActiveConnectionTimerCounter();

                return available;
            }

            private void SetLastActiveConnectionTimerCounter()
            {
                LastActiveConnectionTimerCounter = MainClass.TimeCounter;
            }

            public void CheckTimeoutAndClose(int TimerCounter)
            {
                var timeout = Math.Max(listen.TimeoutReceiveFromClient, Math.Max(listen.TimeoutReceiveFromTarget, Math.Max(listen.TimeoutSendToClient, listen.TimeoutSendToTarget) ));
                if (!isEstablished)
                if (timeout > 120_000)
                    timeout = 120_000;

                if (TimerCounter - LastActiveConnectionTimerCounter > (timeout / 1000))
                {
                    LogForConnection($"Connection closed by watchdog timer", connection, 3);
                    Dispose();
                }
            }

            public void Dispose()
            {
                Dispose(false);
            }

            public bool doTerminate = false;
            public bool isDisposed  = false;
            public void Dispose(bool doNotDelete)
            {
                lock (this)
                {
                    if (isDisposed)
                        return;
    
                    LogForConnection($"Closing connection started; Count of connections in the listener {listen.connections.Count}", connection, 4);
                    try {    if (connection  .Connected) connection  .Shutdown(SocketShutdown.Both);    } catch {}
                    try {    if (connectionTo.Connected) connectionTo.Shutdown(SocketShutdown.Both);    } catch {}
    
                    doTerminate = true;
                    start.Stop();

                    try
                    {
                        if (!doNotDelete)
                        lock (listen.connections)
                            listen.connections.Remove(this);
                    }
                    catch (Exception e)
                    {
                        listen.Log($"Connection.Dispose {connectToSocks} raised exception " + e.Message, 0);
                    }
    
                    try {  connection  ?.Dispose();  } catch {}
                    try {  connectionTo?.Dispose();  } catch {}
                    connection   = null;
                    connectionTo = null;
    
                    LogForConnection($"Connection closed; sended bytes {SizeOfTransferredDataTo.ToString("N0")}, received bytes {SizeOfTransferredDataFrom.ToString("N0")}; time {start.Elapsed}; Count of connections in the listener {listen.connections.Count}", connection, 2);
                    isDisposed = true;
                }

                GC.Collect();
            }
        }
    }
}
