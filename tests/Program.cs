using System;
using static trusts.Helper;
namespace tests
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            if ( !isIPv4("127.0.0.1") )
                Console.WriteLine("ERROR IPv4");
            if ( !isIPv4("77.88.8.88") )
                Console.WriteLine("ERROR IPv4");
            if ( !isIPv4("8.8.4.4") )
                Console.WriteLine("ERROR IPv4");
            if ( !isIPv4("0.1.8.9") )
                Console.WriteLine("ERROR IPv4");

            if ( isIPv4("a.0.0.1") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4("0.0.0.z") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4("1270.0.0.1") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4("127.1") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4("127.0.1") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4("127.0.0.1.") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4(".127.0.0.1") )
                Console.WriteLine("ERROR IPv4");
            if ( isIPv4("ya.ru") )
                Console.WriteLine("ERROR IPv4");

            if ( !isIPv6("::1") )
                Console.WriteLine("ERROR IPv6");

            if ( !isIPv6("fe80::200:f8ff:fe21:67cf") )
                Console.WriteLine("ERROR IPv6");
            if ( isIPv6("fe80::200:f8ff: fe21:67cf") )
                Console.WriteLine("ERROR IPv6");

            if ( !isIPv6("3FFF:FFFF:FFFF:FFFF:FFFF:FFFF:FFFF:FFFF") )
                Console.WriteLine("ERROR IPv6");

            if ( !isIPv6("2000::") )
                Console.WriteLine("ERROR IPv6");

            if ( isIPv6("ab.aa:8080") )
                Console.WriteLine("ERROR IPv6");
            if ( isIPv6("ab:8080") )
                Console.WriteLine("ERROR IPv6");
            if ( !isIPv6("ab::8080") )
                Console.WriteLine("ERROR IPv6");
        }
    }
}
