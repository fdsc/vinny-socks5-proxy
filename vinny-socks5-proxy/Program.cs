using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static vinnysocks5proxy.Helper;

// Стандарт по socks5
// https://datatracker.ietf.org/doc/html/rfc1928

namespace vinnysocks5proxy
{
    partial class MainClass
    {
        public static string version = "2021-0725";

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
                    Log("Incorrect .conf file");
                    return 1;
                }

                Console.WriteLine("starting " + getDateTime());
    
                Console.CancelKeyPress              += Console_CancelKeyPress;
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                AcceptThread = new Thread(AcceptThreadFunc);
                AcceptThread.IsBackground = true;
                AcceptThread.Start();

                do
                {
                    ExitWaitEvent.Reset();
                    ExitWaitEvent.WaitOne();
                }
                while (!toTerminate);
            }
            catch (Exception e)
            {
                isError = true;

                Console.Error.WriteLine("Error occured");
                Console.Error.WriteLine(e.Message);
                Console.Error.WriteLine(e.StackTrace);
            }
            finally
            {
                foreach (var ls in listens)
                    ls.Dispose();
            }

            Console.WriteLine("Terminated " + getDateTime());
            Log("Terminated");

            TerminatedEvent.Set();

            return isError ? 1000 : 0;
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
            do
            {
                var t = Accept(tasks);
                t.Wait();
            }
            while (!toTerminate);
        }

        static async Task Accept(SortedList<ListenConfiguration, Task> tasks)
        {
            foreach (var ls in listens)
            {
                if (!tasks.ContainsKey(ls))
                    AddListenToWaitConnect(tasks, ls);
            }

            var connected = (Task<Socket>) await Task.WhenAny(tasks.Values);

            var index = tasks.IndexOfValue(connected);

            var listen  = tasks.Keys[index];
            tasks.RemoveAt(index);
            AddListenToWaitConnect(tasks, listen);

            listen.newConnection(await connected);
        }

        private static void AddListenToWaitConnect(SortedList<ListenConfiguration, Task> tasks, ListenConfiguration ls)
        {
            var task = ls.Accept();
            if (task != null)
                tasks.Add(ls, task);
        }
    }
}
