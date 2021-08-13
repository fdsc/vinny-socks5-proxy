using System;
using System.Net;

namespace trusts
{
    /// <summary>
    /// Информация о перенаправлении на другой прокси-сервер
    /// </summary>
    public class ForwardingInfo
    {                                                            /// <summary>Строка с IP-адресом</summary>
        public string     forwarding      = null;                /// <summary>Строка с портом для подключения</summary>
        public int        forwardingPort  = 0;                   /// <summary>IP-адрес, полученный после вызова parse()</summary>
        public IPAddress  address         = null;
        
        /// <summary>Функция для парсинга строки с IP-адресом. В случае изменения адреса, нужно приравнять address = null, а затем уже только вызвать данный метод</summary>
        public void parse()
        {
            if (address != null)
                return;

            address = IPAddress.Parse(forwarding);
        }
    }
}
