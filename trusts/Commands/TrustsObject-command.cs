using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Подкоманда команды cmp. Тип команды задаётся полем типа CompareType</summary>
        public class Compare: SubCommand
        {                                                               /// <summary>Тип сравнения (exactly, StartsWith, ...)</summary>
            public readonly CompareType Type        = 0;                /// <summary>Начальный индекс поиска по поддоменам</summary>
            public readonly int         StartIndex  = 0;                /// <summary>Конечный индекс поиска по поддоменам</summary>
            public readonly int         EndIndex    = int.MaxValue;     /// <summary>Режим сравнения (двоеточие или звёздочка)</summary>
            public readonly SplitRegime splitRegime = SplitRegime.inString;
                                                                        /// <summary>Режим разделения строки (двоеточие или звёздочка)</summary>
            public enum SplitRegime
            {                                                           /// <summary>Строка для сравнения является одной строкой (двоеточие)</summary>
                inString = 1,                                           /// <summary>Сравнивается массив строк (звёздочка)</summary>
                splitted = 2
            }
                                                                        /// <summary>Тип сравнения</summary>
            // При изменении типа добавить ниже, в переменную types, перечень допустимых параметров
            public enum CompareType
            {                                                           /// <summary>Ошибочный тип</summary>
                error      = 0,                                         /// <summary>Точное соответствие</summary>
                exactly    = 1,                                         /// <summary>Начинается со строки</summary>
                startsWith = 2,                                         /// <summary>Заканчивается строкой</summary>
                endsWith   = 3,                                         /// <summary>Содержит строку</summary>
                contains   = 4,                                         /// <summary>Соответствие регулярному выражению</summary>
                regex      = 5
            };

            /// <summary>Сопоставление строковых команд целочисленному типу команды. Все команды указываются в НИЖНЕМ РЕГИСТРЕ</summary>
            public static readonly SortedList<string, CompareType> types = new SortedList<string, CompareType>()
            {
                { "exactly",    CompareType.exactly },
                { "startswith", CompareType.startsWith },
                { "endswith",   CompareType.endsWith },
                { "contains",   CompareType.contains },
                { "regex",      CompareType.regex }
            };


            /// <summary>Создание подкоманды для команды cmp</summary>
            /// <param name="command">Вышестоящая команда (cmp). Сюда приходит команда по типу "exactly:d[0:1]"</param>
            public Compare(Command command): base(command)
            {
                // exactly:d[0:1] делим на exactly и d[0:1]
                var splitted = command.Parameter.Split(new string[] {":"}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length != 2)
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :cmp:exactly:d[:])", command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }

                // Команда регистронезависима
                var Name = splitted[0].Trim().ToLowerInvariant();
                
                // Ищем нужный тип команды
                var index = types.IndexOfKey(Name);
                
                if (index < 0)
                {
                    command.syntaxError = true;
                    var sb = new StringBuilder();
                    sb.AppendLine($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :cmp:exactly:d[:])");
                    sb.AppendLine("List of correct parameters name:");
                    foreach (var type in types)
                        sb.AppendLine(type.Key);

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
                else
                    Type = types.Values[index];

                // Если нашли нужный тип, то продолжаем обработку
                // splitted[1] - это команда по типу "d[0]"
                var indexatorString = splitted[1].Trim().ToLowerInvariant();
                if (indexatorString.StartsWith("d["))
                {
                    indexatorString = indexatorString.Substring(startIndex: 2);
                    index = indexatorString.IndexOf(']');
                    if (index <= 0)
                        goto indexatorError;

                    // Сейчас мы получаем то, что внутри скобок без самих скобок
                    indexatorString = indexatorString.Substring(startIndex: 0, length: index).Trim();
                    var cont1 = indexatorString.Contains(":");
                    var cont2 = indexatorString.Contains("*");
                    if (cont1 && cont2)
                        goto indexatorError;

                    if (cont2)
                        splitRegime = SplitRegime.splitted;

                    // Если есть либо ':' либо '*'
                    if (cont1 || cont2)
                    {
                        splitted = indexatorString.Split(new string[] {":", "*"}, StringSplitOptions.None);
                        if (splitted.Length != 2)
                            goto indexatorError;
    
                        var left  = splitted[0].Trim();
                        var right = splitted[1].Trim();
                        // d[:]
                        if (left.Length == 0 && right.Length == 0)
                        {
                            if (indexatorString != ":")
                                goto indexatorError;

                            // Всё уже верно установлено, ничего устанавливать не надо
                        }
                        else
                        // d[1:2]
                        if (left.Length > 0 && right.Length > 0)
                        {
                            try
                            {
                                StartIndex = Int32.Parse(left);
                                EndIndex   = Int32.Parse(right);
                            }
                            catch
                            {
                                goto indexatorError;
                            }
                        }
                        else
                        // d[1:]
                        if (left.Length > 0)
                        {
                            try
                            {
                                StartIndex = Int32.Parse(left);
                            }
                            catch
                            {
                                goto indexatorError;
                            }
                        }
                        else
                        // d[:1]
                        if (right.Length > 0)
                        {
                            try
                            {
                                EndIndex   = Int32.Parse(right);
                            }
                            catch
                            {
                                goto indexatorError;
                            }
                        }
                        else
                            goto indexatorError;    // Такого вообще быть не может
                    }
                    else
                    {
                        try
                        {
                            StartIndex = Int32.Parse(indexatorString);
                            EndIndex   = StartIndex;
                        }
                        catch
                        {
                            goto indexatorError;
                        }
                    }
                }
                else
                    goto indexatorError;

                // command.OwnObject.logger.Log($"cmp:d[{StartIndex}:{EndIndex}", command.OwnObject.Name, ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                return;

                indexatorError:
                {
                    command.syntaxError = true;
                    var sb = new StringBuilder();
                    sb.AppendLine($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :cmp:exactly:d[:])");
                    sb.AppendLine("indexator must be similary to 'd[...]'. Examples");
                    sb.AppendLine("d[:] (entire domain string)");
                    sb.AppendLine("d[0:1] (second level domain string)");
                    sb.AppendLine("d[*] (каждый поддомен перебирается в отдельности)");
                    sb.AppendLine("d[1*]  (каждый поддомен перебирается в отдельности, кроме первого уровня)"); // TODO: перевести
                    sb.AppendLine("d[2*3] (каждый поддомен третьего и четвёртого уровня перебирается в отдельности)"); // TODO: перевести

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
            }
                                                                        /// <summary>Имя подкоманды</summary>
            public override string Name => Type.ToString();
        }
    }
}
