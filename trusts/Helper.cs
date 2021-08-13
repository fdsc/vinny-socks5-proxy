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

        /// <summary>Проверяет на то, является ли символ цифрой (0 ... 9)</summary>
        /// <returns><c>true</c>, если символ является числом</returns>
        /// <param name="c">Проверяемый символ</param>
        public static bool isNumber(char c)
        {
            if (c < '0')
                return false;
            if (c > '9')
                return false;

            return true;
        }

        private static void isNumbers(string address, ref int i, out int cnt)
        {
            cnt = 0;
            while (   i < address.Length && isNumber(address[i])   )
            {
                cnt++;
                i++;

                if (cnt >= 4)
                    return;
            }
        }

        /// <summary>Проверяет, что строка является IP-адресом. Если в группе значение более 255, это всё равно считается корректным IPv4 адресом. Предназначено исключительно для ограничения IPv4-адресов. Строка может и не быть корректным адресом</summary>
        /// <returns><c>true</c>, если address является IPv4-адресом, <c>false</c> иначе. Пробелы считаются недопустимыми символами. Если они допустимы в проверяемой строке, то их нужно вручную убирать перед проверкой.</returns>
        /// <param name="address">Строка, проверяемая на соответствие шаблону IPv4 адреса</param>
        public static bool isIPv4(string address)
        {
            var i = 0;

            // Проходим по всем группам чисел
            for (int g = 0; g < 4; g++)
            {
                isNumbers(address, ref i, out int cnt);

                if (cnt > 3 || cnt <= 0)
                    return false;

                // Требуем точки, кроме последней группы чисел
                if (g < 3)
                    if (i >= address.Length || address[i] != '.')
                        return false;
                    else
                        i++;
            }

            if (i != address.Length)
                return false;

            return true;
        }

        /// <summary>Проверяет символ на то, является ли он шестнадцатеричной цифрой</summary>
        /// <returns><c>true</c>, если символ является шестнадцатеричной цифрой</returns>
        /// <param name="c">Проверяемый символ</param>
        public static bool isHexNumber(char c)
        {
            if (c >= '0' && c <= '9')
                return true;

            if (c >= 'A' && c <= 'F')
                return true;

            if (c >= 'a' && c <= 'f')
                return true;

            return false;
        }

        private static void isHexNumbers(string address, ref int i, out int cnt)
        {
            cnt = 0;
            while (   i < address.Length && isHexNumber(address[i])   )
            {
                cnt++;
                i++;

                if (cnt >= 5)
                    return;
            }
        }

        /// <summary>Проверяет строку на то, что она является IPv6 адресом. Предназначено исключительно для ограничения IPv6-адресов. Строка может и не быть корректным адресом</summary>
        /// <returns><c>true</c>, если строка является IPv6 адресом</returns>
        /// <param name="address">Проверяемая строка</param>
        public static bool isIPv6(string address)
        {
            var i = 0;
            var haveColons = false;

            // Проходим по всем группам чисел
            for (int g = 0; g < 8 && i < address.Length; g++)
            {
                isHexNumbers(address, ref i, out int cnt);
    
                if (cnt > 4)
                    return false;

                // Это значит, что здесь встретилось двоеточие без цифр (либо в начале, либо после другой группы)
                if (cnt == 0)
                {
                    // Двоеточие на месте последней группы не допустимо
                    if (g == 7)
                        return false;

                    // Если это не двоеточие
                    if (i >= address.Length || address[i] != ':')
                        return false;

                    // Уже было один раз двойное двоеточие
                    if (haveColons)
                        return false;

                    haveColons = true;

                    // Требуем два двоеточния в начале. В остальных случаях они и так будут обработаны
                    if (g == 0)
                        i++;
                }

                // Требуем двоеточия, кроме последней группы чисел
                if (g < 7)
                {
                    // Если это конец адреса и
                    // либо имелись все группы, либо имелось двойное двоеточие
                    if (i == address.Length)
                    if (haveColons || g == 7)
                        return true;

                    if (i >= address.Length || address[i] != ':')
                        return false;
                    else
                        i++;
                }
            }

            if (i != address.Length)
                return false;

            return true;
        }
    }
}
