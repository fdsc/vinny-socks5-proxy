using System;
using System.Collections.Generic;

namespace trusts.Commands
{
    /// <summary>Приоритет команды</summary>
    public class Priority
    {
        /// <summary>Содержит значение приоритета. Нулевое число самое важное</summary>
        public readonly List<uint>  Priorities = null;

        /// <summary>Если true, то при работе программы случилась ошибка. После конструктора значение должно быть false</summary>
        public readonly bool syntaxError = true;

        /// <summary>Конструктор</summary><param name="priorityString">Строка приоритетов по типу "0.1.2.3.4"</param>
        public Priority(string priorityString)
        {
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
    }
}
