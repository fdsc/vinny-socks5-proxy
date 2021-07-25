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
        public static bool getFromConfFile(string[] args)
        {
            var confFilePath = Path.GetFullPath("vinny-socks5-proxy.conf");
            if (args.Length > 0)
                confFilePath = Path.GetFullPath(args[0]);

            if (!File.Exists(confFilePath))
            {
                Console.Error.WriteLine("conf file not exists " + confFilePath);
                return false;
            }
            
            var lines_raw = File.ReadLines(confFilePath, new System.Text.UTF8Encoding());
            foreach (var line_raw in lines_raw)
            {
                var line = line_raw.Trim();
                
                if (line.StartsWith("#", StringComparison.InvariantCultureIgnoreCase) || line.Length <= 0)
                    continue;

                var @params = line.Split(new char[] { ' ', '\t', ':', ',', ';' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (@params.Length != 2)
                {
                    Console.Error.WriteLine("error in conf file " + confFilePath);
                    Console.Error.WriteLine(line);
                    Console.Error.WriteLine("Line must have format: name parameter");

                    return false;
                }

                var pName = @params[0].Trim().ToLowerInvariant();
                var pVal  = @params[1].Trim().ToLowerInvariant();
                
                
                switch (pName)
                {
                    case "error":
                        Console.Error.WriteLine("error in conf file " + confFilePath);
                        Console.Error.WriteLine(pVal);
                        return false;
                    case "info":
                        Log(pVal);
                        break;

                    case "listen_address":
                        if (listen_address != null)
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("listen_address may be only one");
                            return false;
                        }

                        listen_address = pVal;
                        break;
                    case "listen_port":
                        listen_port = pVal;
                        break;
                    case "log_file":
                        try
                        {
                            log_file = new FileInfo(pVal);
                            if (!log_file.Exists)
                                File.WriteAllText(log_file.FullName, "");

                            Log("Starting");
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("log_file incorrect: " + pVal);
                            Console.Error.WriteLine(e.Message);
                            return false;
                        }
                        break;
                    case "max_connections":
                        try
                        {
                            max_connections = int.Parse(pVal);
                            if (max_connections < 1)
                            {
                                Console.Error.WriteLine("error in conf file " + confFilePath);
                                Console.Error.WriteLine("max_connections incorrect (must be positive number): " + pVal);
                                return false;
                            }
                        }
                        catch
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("max_connections incorrect (must be number): " + pVal);
                            return false;
                        }

                        break;
                    default:
                        Console.Error.WriteLine("error in conf file " + confFilePath);
                        Console.Error.WriteLine("unknown parameter name: " + pName);
                        return false;
                }
            }
            
            if (listen_address == null || listen_port == null)
            {
                Console.Error.WriteLine("error in conf file " + confFilePath);
				Console.Error.WriteLine("You must set listen_address and listen_port (not setted both or just one)");
                return false;
            }
			
            
            try
            {
                var listen_ip = IPAddress.Parse(listen_address);
                var listen_p  = Int32.Parse(listen_port);
			    listen = new IPEndPoint(listen_ip, listen_p);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("error in conf file " + confFilePath);
                Console.Error.WriteLine("listen_address or listen_port is incorrect");
                Console.Error.WriteLine(e.Message);

                return false;
            }

            listen_socket = new Socket(listen.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            listen_socket.Bind(listen);
            listen_socket.Listen(65536);

            Log("Listen " +  listen.ToString());

            return true;
        }

        public static void Log(string Message)
        {
            if (log_file == null)
                return;

            lock (log_file)
            File.AppendAllText(log_file.FullName, getDateTime() + "\r\n" + Message + "\r\n----------------------------------------------------------------\r\n\r\n");
        }
    }
}
