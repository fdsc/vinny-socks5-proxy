using System;
using System.Collections.Generic;
using System.Text;

namespace trusts.Commands
{
    /// <summary>Приоритет команды</summary>
    public class DomainName
    {
        /// <summary>Имя домена</summary>
        public readonly string   Name     = null;     /// <summary>Имя домена, разделённое по точкам</summary>
        public readonly string[] splitted = null;

        /// <summary>Если true, то при работе программы случилась ошибка. После конструктора значение должно быть false</summary>
        public readonly bool syntaxError = true;
        
        /// <summary>Имя домена</summary>
        public readonly string[] DeniedSymbols = { "@", ":", "/", "..", @"\"};

        /// <summary>Конструктор</summary><param name="DomainName">Имя домена</param>
        public DomainName(string DomainName)
        {
            this.Name = DomainName.ToLowerInvariant();

            if (DomainName == null || DomainName.Trim().Length <= 0)
            {
                syntaxError = true;
                return;
            }

            foreach (var ds in DeniedSymbols)
            {
                if (DomainName.Contains(ds))
                {
                    syntaxError = true;
                    return;
                }
            }

            foreach (var sym in DomainName)
            {
                // Пробелы и другие служебные символы в доменном имени запрещены
                if (sym <= 32 || sym >= 128)
                {
                    syntaxError = true;
                    return;
                }
            }

            splitted = Name.Split('.');
            Array.Reverse(splitted);

            syntaxError = false;
        }

        /// <summary>Возвращает подстроку, содержащую только указанные уровни поддоменов</summary>
        /// <param name="s">Начальный индекс уровня поддомена</param>
        /// <param name="e">Конечный индекс уровня поддомена (включительно)</param>
        public string this[int s, int e]
        {
            get
            {
                bool first = true;
                var sb = new StringBuilder();
                for (int i = s; i <= e; i++)
                {
                    if (i >= splitted.Length)
                        break;

                    if (first)
                    {
                        sb.Append(splitted[i]);
                        first = false;
                    }
                    else
                        sb.Insert(0, splitted[i] + ".");
                }

                return sb.ToString();
            }
        }
    }
}
