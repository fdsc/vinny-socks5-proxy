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
            public bool ConnectByIP(IPAddress toIP, int ConnectToPort, ref bool connected, ref int networkUnreachable, ref int connectionRefused, ref int anotherError)
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
        }
    }
}
