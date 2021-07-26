using System;
using System.Text;

namespace trusts
{
    /// <summary>Дополнительный класс-помошник</summary>
    public static class Helper
    {
        /// <summary>Выдаёт дату и время для лога</summary>
        /// <returns>Строка с текущим датой и временем</returns>
        public static string getDateTime()
        {
            var now  = DateTime.Now;
            var date = now.Year.ToString("D4") + "." + now.Month .ToString("D2") + "." + now.Day   .ToString("D2");
            var time = now.Hour.ToString("D2") + ":" + now.Minute.ToString("D2") + ":" + now.Second.ToString("D2") + "." + now.Millisecond.ToString("D3");

            return date + " " + time;
        }
        
        /// <summary>Безопасно с точки зрения тайминг-атак сравнивает две строки</summary>
        /// <returns><c>true</c>, если две строки равны, <c>false</c> если две строки не равны.</returns>
        /// <param name="s1">Первая строка для сравнения</param>
        /// <param name="s2">Втроая строка для сравнения</param>
        public static bool SecureCompare(string s1, string s2)
        {
            var len = s1.Length;
            if (s2.Length < len)
                len = s2.Length;

            var result = 0;
            for (int i = 0; i < len; i++)
            {
                result |= s1[i] ^ s2[i];
            }

            return result == 0 && s1.Length == s2.Length;
        }
    }
}
