using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static vinnysocks5proxy.Helper;
using static trusts.Helper;

// Стандарт по socks5
// https://datatracker.ietf.org/doc/html/rfc1928

namespace vinnysocks5proxy
{
    partial class MainClass
    {
        public static string version = "2021-0813";

        public static bool   toTerminate = false;
        public static bool   isError     = false;

        public static List<ListenConfiguration>  listens = new List<ListenConfiguration>();

        public static FileInfo         log_file          = null;        
        public static ManualResetEvent ExitWaitEvent   = new ManualResetEvent(false);
        public static ManualResetEvent TerminatedEvent = new ManualResetEvent(false);

        public static Thread AcceptThread = null;

        public static int Main(string[] args)
        {
            try
            {
                if (!getFromConfFile(args) || args.Length >= 2)
                {
                    Console.WriteLine(getHelpString());
                    Log("Incorrect .conf file (or error at open conf file or log file)");
                    return 1;
                }

                Console.WriteLine("starting " + getDateTime());
    
                try
                {
                    Console.CancelKeyPress               += Console_CancelKeyPress;
                    AppDomain.CurrentDomain.ProcessExit  += CurrentDomain_ProcessExit;
                    AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Error occured");
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);
                    Log(e.Message + "\r\n" + e.StackTrace);
                }

                AcceptThread = new Thread(AcceptThreadFunc);
                AcceptThread.IsBackground = true;
                AcceptThread.Start();

                var timer = new System.Timers.Timer();
                timer.Interval  = 1000;
                timer.AutoReset = true;
                timer.Elapsed += Timer_Elapsed;
                timer.Start();

                do
                {
                    ExitWaitEvent.Reset();
                    ExitWaitEvent.WaitOne();
                }
                while (!toTerminate);
                Console.WriteLine("Start termination... " + getDateTime());
                timer.Stop();
            }
            catch (Exception e)
            {
                isError = true;

                Console.Error.WriteLine("Error occured");
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
                Log(e.Message + "\r\n" + e.StackTrace);
            }
            finally
            {
                Parallel.ForEach
                (
                    listens,
                    delegate (ListenConfiguration ls)
                    {
                        try
                        {
                            ls.Dispose();
                        }
                        catch
                        {}
    	            }
                );
            }

            Console.WriteLine("Terminated " + getDateTime());
            Log("Terminated");

            TerminatedEvent.Set();

            return isError ? 1000 : 0;
        }

        public static volatile int TimeCounter = 0;
        static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (toTerminate)
                return;

            lock (ExitWaitEvent)
            {
                TimeCounter++;

                // Считаем остаток от деления на 128
                var tm = TimeCounter & 0x7F;
                if (tm == 0)    // Каждые две минуты, примерно
                {
                    for (int i = 0; i < listens.Count; i++)
                    {
                        var ls  = listens[i];
                        var cnt = ls.connections.Count;
                        for (var j = 0; j < ls.connections.Count; j++)
                        {
                            var connection = ls.connections[j];
                            try
                            {
                                if (connection.CheckTimeoutAndClose(TimeCounter))
                                    i--;
                            }
                            catch
                            {}
                        }

                        ls.Log("Watchdog timer: Count of connections in the listener " + ls.connections.Count, (cnt != ls.connections.Count || ls.connections.Count > 0) ? 2 : 3, trusts.ErrorReporting.LogTypeCode.Usually);
                    }
                }
            }
        }

        /// <summary>Устанавливаем флаг и событие для сообщения всем о том, что мы должны завершиться</summary>
        public static void doTerminate()
        {
            toTerminate = true;
            ExitWaitEvent.Set();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            doTerminate();

            // Ждём TerminatedEvent, т.к. иначе процесс завершится при выходе из обработчика и не успеет провести корректный выход
            // Впрочем, здесь он и так ничего не успевает, похоже
            TerminatedEvent.WaitOne();
        }

        static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            doTerminate();
            TerminatedEvent.WaitOne();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            doTerminate();
            TerminatedEvent.WaitOne();
        }

        static void AcceptThreadFunc()
        {
            SortedList<ListenConfiguration, Task> tasks = new SortedList<ListenConfiguration, Task>(16);

            var sb = new StringBuilder();
            sb.AppendLine("Try to srart listeners:");
            foreach (var ls in listens)
            {
                sb.AppendLine(ls.ListenAddressForLog);
                if (ls.logger.LogFileName != null)
                    sb.AppendLine(ls.logger.LogFileName);
                else
                    sb.AppendLine("no log file");
            }
            sb.AppendLine("(result see in listeners logs)");
            Log(sb.ToString());

            int count = 0;
            DateTime lastTimeOfError = default;
            do
            {
                try
                {
                    var t = Accept(tasks);
                    t.Wait();
                }
                // Это случается, когда программа завершается
                catch (ThreadAbortException e)
                {
                    if (toTerminate)
                        return;

                    Log("Error with listener: " + e.Message + "\r\n\r\n" + e.StackTrace);
                    doTerminate();
                    return;
                }
                catch (Exception e)
                {
                    count++;
                    Log("Error with listener: " + e.Message + "\r\n\r\n" + e.StackTrace);

                    var now = DateTime.Now;
                    if ((now - lastTimeOfError).TotalSeconds > 60)
                    {
                        count--;
                        if ((now - lastTimeOfError).TotalSeconds > 3600)
                            count = 0;
                    }
                    lastTimeOfError = now;
                }
            }
            while (!toTerminate && count < 64 && listens.Count > 0);

            if (!toTerminate && (listens.Count <= 0 || count >= 64))
            {
                MainClass.Log("Terminating by errors");
            }

            doTerminate();
        }

        static async Task Accept(SortedList<ListenConfiguration, Task> tasks)
        {
            if (listens.Count <= 0)
                return;

            foreach (var ls in listens)
            {
                try
                {
                    if (!ls.Incorrect)
                    if (!tasks.ContainsKey(ls))
                        AddListenToWaitConnect(tasks, ls);
                }
                catch (SocketException e)
                {
                    Log("Connection error in the general listener function (removed from listen): " + ls.ListenAddressForLog + "\r\n" + e.Message);
                    ls.Incorrect = true;
                }
                catch (Exception e)
                {
                    Log("Error in the general listener function (removed from listen): " + ls.ListenAddressForLog + "\r\n" + e.Message + "\r\n\r\n" + e.StackTrace);
                    ls.Incorrect = true;
                }
            }
            
            for (var i = 0; i < listens.Count; i++)
            {
                var ls = listens[i];
                if (ls.Incorrect)
                {
                    try {  ls.Dispose();  } catch { }
                    listens.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            if (listens.Count <= 0)
                return;

            try
            {
                var connected = (Task<Socket>) await Task.WhenAny(tasks.Values);
    
                var index = tasks.IndexOfValue(connected);

                var listen  = tasks.Keys[index];
                try
                {
                    tasks.RemoveAt(index);
                    AddListenToWaitConnect(tasks, listen);
    
                    listen.newConnection(await connected);
                }
                // Это случается, когда программа завершается
                catch (ThreadAbortException e)
                {
                    if (toTerminate)
                        return;

                    Log("Error with accept connection \r\n" + e.Message);
                    return;
                }
                catch (Exception ex)
                {
                    if (toTerminate)
                        return;

                    Log("Error with accept connection \r\n" + ex.Message + "\r\n\r\n" + ex.StackTrace);
                }
            }
            catch (Exception e)
            {
                Log("Error with listen connections \r\n" + e.Message + "\r\n\r\n" + e.StackTrace);
            }
        }

        private static void AddListenToWaitConnect(SortedList<ListenConfiguration, Task> tasks, ListenConfiguration ls)
        {
            var task = ls.Accept();
            if (task != null)
                tasks.Add(ls, task);
        }
    }
}
