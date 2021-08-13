using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Команда перенаправления на другой прокси</summary>
        public class ForwardCommand: SubCommand
        {                                                               /// <summary>Тип перенаправления</summary>
            public readonly ForwardType Type = 0;
                                                                        /// <summary>Тип перенаправления</summary>
            // При изменении типа добавить ниже, в переменную types, перечень допустимых параметров
            public enum ForwardType
            {                                                           /// <summary>Ошибочный тип</summary>
                error      = 0,                                         /// <summary>Перенаправление через socks5. Другие типы перенаправлений не поддерживаются</summary>
                socks5     = 1
            };

            /// <summary>Сопоставление строковых команд целочисленному типу команды. Все команды указываются в НИЖНЕМ РЕГИСТРЕ</summary>
            public static readonly SortedList<string, ForwardType> types = new SortedList<string, ForwardType>()
            {
                { "socks5", ForwardType.socks5 }
            };


            /// <summary>Команда перенаправления на другой прокси</summary>
            /// <param name="command">Вышестоящая команда (cmp). Сюда приходит команда по типу "socks:8080:127.0.0.1"</param>
            /// <param name="LineNumber">Номер строки, на которой встречена данная лексема</param>
            public ForwardCommand(Directive command, int LineNumber): base(command, LineNumber)
            {
                command.syntaxError = true;

                // Здесь строка, идущая после forward:
                var splitted = command.Parameter.Split(new string[] {":"}, 3, StringSplitOptions.None);
                if (splitted.Length < 3)
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Command '{command.Name}' at line {LineNumber} contains incorrect parameter '{command.Parameter}' (example: :forward:socks5:8080:127.0.0.1)", command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }

                // Команда регистронезависима
                var strType = splitted[0].Trim().ToLowerInvariant();

                // Ищем нужный тип команды
                var index = types.IndexOfKey(strType);

                if (index < 0)
                {
                    command.syntaxError = true;
                    var sb = new StringBuilder();
                    sb.AppendLine($"Command '{command.Name}' at line {LineNumber} contains incorrect parameter '{command.Parameter}' (example :forward:socks5:8080:127.0.0.1)");
                    sb.AppendLine("List of correct parameters name:");
                    foreach (var type in types)
                        sb.AppendLine(type.Key);

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
                else
                    Type = types.Values[index];

                var inv = splitted[1].Trim().ToLowerInvariant();
                if (inv != "none")
                {
                    fi = new ForwardingInfo();
                    fi.forwarding     = splitted[2].Trim();
                    fi.forwardingPort = int.Parse(splitted[1].Trim());
    
                    try
                    {
                        fi.parse();
                    }
                    catch (Exception ex)
                    {
                        command.syntaxError = true;
                        command.OwnObject.logger.Log($"Command '{command.Name}' at line {LineNumber} contains incorrect parameters, error '{ex.Message}' (example: :forward:socks5:8080:127.0.0.1)", command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                        return;
                    }
                }

                command.syntaxError = false;
            }

            public readonly ForwardingInfo fi = null;

                                                                        /// <summary>Имя подкоманды</summary>
            public override string Name => Type.ToString();

            /// <summary>Добавляет параметр команды (отдельная строка, идущая ниже команды)</summary>
            /// <param name="tLine">Строка параметра команды</param>
            public override void addParameter(string tLine)
            {
                // parametres.Add(tLine.Trim());
                base.addParameter(tLine);
                
                switch (Type)
                {
                    case ForwardType.socks5:
                        command.syntaxError = true;
                        break;
                    default:
                        command.syntaxError = true;
                        return;
                }
            }
        }
    }
}
