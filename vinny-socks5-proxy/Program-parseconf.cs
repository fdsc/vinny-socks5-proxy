using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using static vinnysocks5proxy.Helper;
using static trusts.Helper;
using trusts;

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
            
            ListenConfiguration current = null;
            string              curUser = null;

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
                var pVal  = @params[1].Trim();
                
                
                switch (pName)
                {
                    case "error":
                        Console.Error.WriteLine("error in conf file " + confFilePath);
                        Console.Error.WriteLine(pVal);
                        return false;

                    case "info":
                        if (current == null)
                            Log(pVal);
                        else
                            current.Log(Replace(pVal, current), 0);
                        break;

                    case "listen":
                        try
                        {
                            current = new ListenConfiguration();
                            curUser = null;

                            listens.Add(current);
                            current.SetAddress(pVal);
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("listen address is incorrect (example: listen 127.0.0.1)");
                            Console.Error.WriteLine(e.Message);
                            return false;
                        }

                        break;
                        
                    case "domain_trusts":
                        try
                        {
                            if (!CheckCurrentAndPrintError(current, confFilePath))
                                return false;

                            current.trusts_domain = new TrustsFile(pVal, current.logger);
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine($"domain_trusts is incorrect: '{pVal}'");
                            Console.Error.WriteLine(e.Message);
                            return false;
                        }

                        break;

                    case "port":
                        try
                        {
                            if (!CheckCurrentAndPrintError(current, confFilePath) || !current.SetPort(int.Parse(pVal)))
                                return false;
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("listen port is incorrect (example: port 1080)");
                            Console.Error.WriteLine(e.Message);
                            return false;
                        }

                        break;

                    case "user":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;

                        curUser = pVal.Trim();

                        break;

                    case "pwd":
                    case "pass":
                    case "passwd":
                    case "password":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;

                        if (curUser == null)
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("user must be specified before password record: " + line);
                            return false;
                        }

                         current.users.Add(curUser, pVal.Trim());
                         curUser = null;
                            
                        break;

                    case "ipv4":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (pVal == "reject")
                            current.namesGranted_ipv4 = false;
                        else
                        if (pVal == "accept")
                            current.namesGranted_ipv4 = true;
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("ipv4 must be 'accept' or 'reject' but have " + pVal);
                            return false;
                        }
                        break;

                    case "ipv6":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (pVal == "reject")
                            current.namesGranted_ipv6 = false;
                        else
                        if (pVal == "accept")
                            current.namesGranted_ipv6 = true;
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("ipv4 must be 'accept' or 'reject' but have " + pVal);
                            return false;
                        }
                        break;

                    case "domain":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;

                        if (pVal == "reject")
                            current.namesGranted_domain = false;
                        else
                        if (pVal == "accept")
                            current.namesGranted_domain = true;
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("ipv4 must be 'accept' or 'reject' but have " + pVal);
                            return false;
                        }
                        break;

                    forwarding_error:
                        Console.Error.WriteLine("forwarding configuration error: only socks5 support. Example: socks5:8080:127.0.0.1 (socks5:port:address)");
                        Console.Error.WriteLine("error in conf file " + confFilePath);
                        return false;

                    case "forward":
                    case "forwarding":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;

                        if (!pVal.StartsWith("socks5:"))
                            goto forwarding_error;

                        var addr = pVal.Split(new string[] { ":" }, 3, StringSplitOptions.None);
                        if (addr.Length != 3)
                            goto forwarding_error;

                        try
                        {
                            current.forwardingPort = int.Parse(addr[1].Trim());
                            current.forwarding     = addr[2].Trim();

                            if (  !isIPv4(current.forwarding)  )
                            {
                                Console.WriteLine("forwarding is supported only IPv4 addresses");
                                goto forwarding_error;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine(ex.Message);
                            goto forwarding_error;
                        }

                        break;

                    case "Timeoutsendtotarget":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (Int32.TryParse(pVal, out int TimeoutSendToTarget))
                        {
                            current.TimeoutSendToTarget = TimeoutSendToTarget;
                        }
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("debug level is a number, but " + pVal);
                            return false;
                        }

                        break;

                    case "timeoutsendtoclient":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (Int32.TryParse(pVal, out int TimeoutSendToClient))
                        {
                            current.TimeoutSendToClient = TimeoutSendToClient;
                        }
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("debug level is a number, but " + pVal);
                            return false;
                        }

                        break;

                    case "timeoutreceivefromclient":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (Int32.TryParse(pVal, out int TimeoutReceiveFromClient))
                        {
                            current.TimeoutReceiveFromClient = TimeoutReceiveFromClient;
                        }
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("debug level is a number, but " + pVal);
                            return false;
                        }

                        break;

                    case "timeoutreceivefromtarget":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (Int32.TryParse(pVal, out int TimeoutReceiveFromTarget))
                        {
                            current.TimeoutReceiveFromTarget = TimeoutReceiveFromTarget;
                        }
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("debug level is a number, but " + pVal);
                            return false;
                        }

                        break;

                    case "debug":
                        if (!CheckCurrentAndPrintError(current, confFilePath))
                            return false;
                            
                        if (Int32.TryParse(pVal, out int debugLevel))
                        {
                            current.debug = debugLevel;
                        }
                        else
                        {
                            Console.Error.WriteLine("error in conf file " + confFilePath);
                            Console.Error.WriteLine("debug level is a number, but " + pVal);
                            return false;
                        }

                        break;

                    case "log_file":
                        try
                        {
                            if (current == null)
                            {
                                log_file = new FileInfo(pVal);
                                if (!log_file.Exists)
                                    File.WriteAllText(log_file.FullName, "");
                            }
                            else
                            {
                                if (current.logger.LogFileName != null)
                                {
                                    Console.Error.WriteLine("error in conf file " + confFilePath);
                                    Console.Error.WriteLine("twice set of log file name " + pVal);
                                    return false;
                                }

                                current.logger.SetLogFileName(  Replace(pVal, current)  );
                                if (!log_file.Exists)
                                    File.WriteAllText(log_file.FullName, "");
                            }
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
                            if (!CheckCurrentAndPrintError(current, confFilePath))
                                return false;

                            current.max_connections = int.Parse(pVal);
                            if (current.max_connections < 1)
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

            current = null;
            
            foreach (var ls in listens)
            {
                if (!ls.checkCorrect())
                {
                    Console.Error.WriteLine("error in conf file " + confFilePath);
                    Console.Error.WriteLine("You must set listen_address and listen_port (not setted both or just one)");
                    Console.Error.WriteLine(ls.listen_ip.ToString());

                    ls.Log("Incorrect address in config", 0);

                    return false;
                }
            }

            foreach (var ls in listens)
            {
                ls.Listen();
            }

            return true;
        }
        
        public static bool CheckCurrentAndPrintError(ListenConfiguration current, string confFilePath)
        {
            if (current != null)
                return true;

            Console.Error.WriteLine("error in conf file " + confFilePath);
            Console.Error.WriteLine("current listen not setted, but an option occured for listen (insert listen option before another options)");
            
            return false;
        }

        public static string Replace(string toReplace, ListenConfiguration current)
        {
            var str = toReplace;
            if (current == null)
            {
                str = str.Replace("$$$addr$$$", "(error:null)");
                str = str.Replace("$$$port$$$", "(error:null)");
                return str;
            }

            if (current.ipe != null)
                str = str.Replace("$$$addr$$$", current.ipe?.ToString());
            else
                str = str.Replace("$$$addr$$$", "[" + current.listen_ip.ToString() + "]:" + current.port);
                
            str = str.Replace("$$$port$$$", current.port.ToString());

            return str;
        }

        public static void Log(string Message)
        {
            if (log_file == null)
                return;

            lock (log_file)
            File.AppendAllText(log_file.FullName, getDateTime() + $";  pid = {System.Diagnostics.Process.GetCurrentProcess().Id}\r\n" + Message + "\r\n----------------------------------------------------------------\r\n\r\n");
        }
    }
}
