using System;
using System.Collections.Generic;

namespace trusts.Commands
{
    /// <summary>Приоритет команды</summary>
    public class Priority: IComparable<Priority>
    {
        /// <summary>Содержит значение приоритета. Нулевое число самое важное</summary>
        public readonly List<uint>  Priorities = null;

        /// <summary>Если true, то при работе программы случилась ошибка. После конструктора значение должно быть false</summary>
        public readonly bool syntaxError = true;

        /// <summary>Конструктор</summary><param name="priorityString">Строка приоритетов по типу "0.1.2.3.4"</param>
        public Priority(string priorityString)
        {
            // Особый служебный приоритет
            if (priorityString == null)
            {
                Priorities = new List<uint>(0);
                syntaxError = false;
                return;
            }

            var splitted = priorityString.Split(new string[] { "." }, StringSplitOptions.None);

            if (splitted.Length < 1)
            {
                return;
            }

            try
            {
                Priorities = new List<uint>(splitted.Length);

                foreach (var ps in splitted)
                {
                    uint subPriority = uint.Parse(ps.Trim());
                    Priorities.Add(subPriority);
                }
            }
            catch
            {
                return;
            }

            syntaxError = false;
        }

        /// <summary>Сравнивает два объекта</summary>
        /// <returns>Возвращает значение > 0, если this > other</returns>
        /// <param name="other">Объект для сравнения с this</param>
        public int CompareTo(Priority other)
        {
            var len1 = this .Priorities.Count;
            var len2 = other.Priorities.Count;
            
            var len  = Math.Min(len1, len2);
            
            for (int i = 0; i < len; i++)
            {
                if (this.Priorities[i] != other.Priorities[i])
                {
                    return (int) this.Priorities[i] - (int) other.Priorities[i];
                }
            }

            return len1 - len2;
        }
    }
}
