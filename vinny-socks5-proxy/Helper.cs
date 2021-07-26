using System;
using System.Text;

namespace vinnysocks5proxy
{
    public static class Helper
    {
        public static string getHelpString()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("version: " + vinnysocks5proxy.MainClass.version);
            sb.AppendLine("vinny-socks5-proxy config_file_path");

            return sb.ToString();
        }
    }
}
