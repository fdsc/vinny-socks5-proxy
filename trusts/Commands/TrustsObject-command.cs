using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using trusts.Commands;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Подкоманда команды cmp. Тип команды задаётся полем типа CompareType</summary>
        public class Command: SubCommand
        {                                                               /// <summary>Тип команды (accept, reject)</summary>
            public readonly CommandType Type     = 0;                   /// <summary>Приоритет команды. 0 - самый низкий</summary> 
            public readonly Priority    Priority = null;
                                                                        /// <summary>Тип команды</summary>
            // При изменении типа добавить ниже, в переменную types, перечень допустимых параметров
            public enum CommandType
            {                                                           /// <summary>Ошибочный тип</summary>
                error      = 0,                                         /// <summary>Разрешить</summary>
                accept     = 1,                                         /// <summary>Запретить</summary>
                reject     = 2
            };

            /// <summary>Сопоставление строковых команд целочисленному типу команды. Все команды указываются в НИЖНЕМ РЕГИСТРЕ</summary>
            public static readonly SortedList<string, CommandType> types = new SortedList<string, CommandType>()
            {
                { "accept", CommandType.accept },
                { "grant",  CommandType.accept  },
                { "allow",  CommandType.accept  },
                { "reject", CommandType.reject  },
                { "deny",   CommandType.reject  },
                { "denied", CommandType.reject  }
            };


            /// <summary>Создание подкоманды для команды command</summary>
            /// <param name="command">Вышестоящая команда (command). Сюда приходит команда по типу "command:accept:0.1"</param>
            public Command(Directive command): base(command)
            {
                // accept:1 делим на exactly и 1
                var splitted = command.Parameter.Split(new string[] {":"}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length != 2)
                {
                    command.syntaxError = true;
                    var str = $"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :command:accept:0.0)";
                    str += "\r\nString must be priority at end (accept:0)";
                    command.OwnObject.logger.Log(str, command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
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
                    sb.AppendLine($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :command:accept:0.0)");
                    sb.AppendLine("List of correct parameters name:");
                    foreach (var type in types)
                        sb.AppendLine(type.Key + "\t(" + type.Value + ")");

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
                else
                    Type = types.Values[index];

                // Если нашли нужный тип, то продолжаем обработку
                Priority = new Priority(splitted[1]);
                if (Priority.syntaxError)
                    goto priorityError;

                // command.OwnObject.logger.Log($"cmp:d[{StartIndex}:{EndIndex}", command.OwnObject.Name, ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                command.syntaxError = false;
                return;

                priorityError:
                {
                    command.syntaxError = true;
                    var sb = new StringBuilder();
                    sb.AppendLine($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :command:accept:0.0)");
                    sb.AppendLine("Priority must be similary to '0.1.2.3.4'");
                    sb.AppendLine("0 - lowest");
                    sb.AppendLine("0.0 > 0");
                    sb.AppendLine("0.1 > 0.0");
                    sb.AppendLine("1.0 > 0.1");

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
            }
                                                                        /// <summary>Имя подкоманды</summary>
            public override string Name => Type.ToString();
        }
    }
}
