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
            public Socket connection   = null;
            public Socket connectionTo = null;
            public long   SizeOfTransferredDataTo   = 0;
            public long   SizeOfTransferredDataFrom = 0;
            public readonly ListenConfiguration listen = null;

            public int waitAvailableBytes(int minBytes = 1, int timeout = 100, int countOfTimeouts = 100)
            {
                int available = 0;
                lock (connection)
                {
                        available = connection.Available;
                    int count     = 0;
                    while (available < minBytes)
                    {
                        Monitor.Wait(connection, timeout);
    
                        available = connection.Available;
    
                        if (count > countOfTimeouts)
                        {
                            listen.Log($"error for connection {connection.RemoteEndPoint.ToString()}: not enought bytes; available {available}");
                            // this.Dispose();
                            return 0;
                        }
                    }
                }

                return available;
            }

            public void Dispose()
            {
                Dispose(false);
            }

            public bool doTerminate = false;
            public void Dispose(bool doNotDelete)
            {
                doTerminate = true;
                listen.LogForConnection($"Connection closed; sended bytes {SizeOfTransferredDataTo}, received bytes {SizeOfTransferredDataFrom}", connection);

                if (!doNotDelete)
                lock (listen.connections)
                    listen.connections.Remove(this);

                connection  ?.Dispose();
                connectionTo?.Dispose();
                connection   = null;
                connectionTo = null;
            }
        }
    }
}
