using System;
using System.Text;

namespace vinnysocks5proxy
{
    public static class Helper
    {
        public static string getDateTime()
        {
            var now  = DateTime.Now;
            var date = now.Year.ToString("D4") + "." + now.Month .ToString("D2") + "." + now.Day   .ToString("D2");
            var time = now.Hour.ToString("D2") + ":" + now.Minute.ToString("D2") + ":" + now.Second.ToString("D2") + "." + now.Millisecond.ToString("D3");

            return date + " " + time;
        }

        
        public static string getHelpString()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("version: " + vinnysocks5proxy.MainClass.version);
            sb.AppendLine("vinny-socks5-proxy config_file_path");

            return sb.ToString();
        }
    }
}
