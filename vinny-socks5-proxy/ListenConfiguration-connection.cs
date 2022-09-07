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

            public class ConnectionSpeedRecord
            {
                public ConnectionSpeedRecord(long dataSize)
                {
                    this.size = dataSize;
                    this.time = DateTime.Now;
                }

                public long     size;
                public DateTime time;
            }

            protected List<ConnectionSpeedRecord> List_SpeedOfConnectionTo   = new List<ConnectionSpeedRecord>(128);
            protected List<ConnectionSpeedRecord> List_SpeedOfConnectionFrom = new List<ConnectionSpeedRecord>(128);
            public long SpeedOfConnectionTo
            {
                get
                {
                    lock (List_SpeedOfConnectionTo)
                    {
                        var now = DateTime.Now;

                        clearListOfSpeedRecords(List_SpeedOfConnectionTo, now);
                        return getSummOfListSpeedRecords(List_SpeedOfConnectionTo, now);
                    }
                }
                set
                {
                    var newRecord  = new ConnectionSpeedRecord(value);

                    lock (List_SpeedOfConnectionTo)
                    {
                        SizeOfTransferredDataTo += value;
                        
                        clearListOfSpeedRecords(List_SpeedOfConnectionTo, newRecord.time);
                        List_SpeedOfConnectionTo.Add(newRecord);
                    }
                }
            }
            
            public long SpeedOfConnectionFrom
            {
                get
                {
                    lock (List_SpeedOfConnectionFrom)
                    {
                        var now = DateTime.Now;

                        clearListOfSpeedRecords(List_SpeedOfConnectionFrom, now);
                        return getSummOfListSpeedRecords(List_SpeedOfConnectionFrom, now);
                    }
                }
                set
                {
                    var newRecord  = new ConnectionSpeedRecord(value);

                    lock (List_SpeedOfConnectionFrom)
                    {
                        SizeOfTransferredDataFrom += value;
                        
                        clearListOfSpeedRecords(List_SpeedOfConnectionFrom, newRecord.time);
                        List_SpeedOfConnectionFrom.Add(newRecord);
                    }
                }
            }

            readonly long secondOfTime     = 10000*1000;   // 10000*1000 - 1 секунда
            readonly long obsolescenceTime = 60*secondOfTime;

            protected long getSummOfListSpeedRecords(List<ConnectionSpeedRecord> list, DateTime now)
            {
                lock (list)
                {
                    if (list.Count <= 0)
                        return 0;

                    var earliestTime = list[0].time;
                    var duration     = now.Ticks - earliestTime.Ticks;
                    
                    if (duration <= 0)
                        return 0;


                    long summOfDataSizes = 0;                    
                    foreach (var item in list)
                    {
                        summOfDataSizes += item.size;
                    }

                    return (summOfDataSizes * secondOfTime / duration);
                }
            }

            protected void clearListOfSpeedRecords(List<ConnectionSpeedRecord> list, DateTime now)
            {
                lock (list)
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    if (item.time.Ticks + obsolescenceTime < now.Ticks)
                    {
                        list.RemoveAt(i);
                        i--;
                    }
                }
            }

            public bool   isEstablished = false;

            /// <summary>Время последней активности по счётчику MainClass.TimeCounter</summary>
            public volatile int LastActiveConnectionTimerCounter = 0;

            public int waitAvailableBytes(Socket connection, int minBytes = 1, int timeout = 100, int countOfTimeouts = 100)
            {
                int available = 0;
                // lock (connection)
                {
                    available = connection?.Available ?? 0;
                    int count = 0;
                    while (available < minBytes)
                    {
                        try { lock (connection) Monitor.Wait(connection, timeout); } catch { }
                        // Thread.Sleep(timeout);

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

            public bool CheckTimeoutAndClose(int TimerCounter)
            {
                var timeout = Math.Max(listen.TimeoutReceiveFromClient, Math.Max(listen.TimeoutReceiveFromTarget, Math.Max(listen.TimeoutSendToClient, listen.TimeoutSendToTarget) ));
                if (!isEstablished)
                if (timeout > 120_000)
                    timeout = 120_000;

                if (TimerCounter - LastActiveConnectionTimerCounter > (timeout / 1000))
                {
                    LogForConnection($"Connection closed by watchdog timer", connection, 3);
                    Dispose();
                    return true;
                }

                return false;
            }

            public void Dispose()
            {
                Dispose(false);
            }

            public volatile bool doTerminate = false;
            public volatile bool isDisposed  = false;
            public void Dispose(bool doNotDelete)
            {
                doTerminate = true;
                lock (this)
                {
                    if (isDisposed)
                        return;
    
                    LogForConnection($"Closing connection started; Count of connections in the listener {listen.connections.Count}", connection, 4);
                    try {    if (connection  .Connected) connection  .Shutdown(SocketShutdown.Both);    } catch {}
                    try {    if (connectionTo.Connected) connectionTo.Shutdown(SocketShutdown.Both);    } catch {}

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

                    LogForConnection($"Connection closed; sended bytes {FormatWithSpaces(SizeOfTransferredDataTo)}, received bytes {FormatWithSpaces(SizeOfTransferredDataFrom)}; time {start.Elapsed}; Count of connections in the listener {listen.connections.Count}", connection, 2);
                    isDisposed = true;
                }

                GC.Collect();
            }
            
            /// <summary>Делает примерно то же, что и SizeOfTransferredDataTo.ToString("N0")</summary>
            /// <returns>Число, дополненное пробелами-разделителями разрядов</returns>
            /// <param name="number">Число для преобразования</param>
            public static string FormatWithSpaces(long number)
            {
                var cnt = 0;
                var sb  = new StringBuilder(8);
                do
                {
                    var t = number % 10;
                    number /= 10;

                    sb.Insert(0, t);

                    cnt++;
                    if (cnt % 3 == 0)
                        sb.Insert(0, " ");
                }
                while (number > 0);

                return sb.ToString();
            }
        }
    }
}
