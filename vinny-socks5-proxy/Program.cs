using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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

        public static Socket listen_socket = null;


        public static string     listen_address  = null;
        public static string     listen_port     = null;
        public static FileInfo   log_file        = null;
        public static int        max_connections = 64;
        public static IPEndPoint listen          = default;

        public static ManualResetEvent ExitWaitEvent   = new ManualResetEvent(false);
        public static ManualResetEvent TerminatedEvent = new ManualResetEvent(false);

        public static Thread AcceptThread = null;

        public static int Main(string[] args)
        {
            try
            {
                if (!getFromConfFile(args))
                {
                    Console.WriteLine(getHelpString());
                    Log("Incorrect .conf file");
                    return 1;
                }
    
                Console.WriteLine("starting " + getDateTime());
    
                Console.CancelKeyPress              += Console_CancelKeyPress;
                AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                AcceptThread = new Thread(AcceptThreadFunc);
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
                listen_socket?.Dispose();
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
            do
            {
                var connection = listen_socket.Accept();
                
                ThreadPool.QueueUserWorkItem
                (
                    delegate
                    {
                        
	                }
                );
            }
            while (!toTerminate);
        }

    }
}
