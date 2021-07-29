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
        public class Transition: SubCommand
        {                                                               /// <summary>Тип команды (ret, call, jmp)</summary>
            public readonly TransitionType Type      = 0;               /// <summary>Приоритет команды. 0 - самый низкий</summary> 
            public readonly Priority       Priority  = null;            /// <summary>Параметр команды</summary>
            public readonly string         Parameter = null;
                                                                        /// <summary>Тип команды</summary>
            // При изменении типа добавить ниже, в переменную types, перечень допустимых параметров
            public enum TransitionType
            {                                                           /// <summary>Ошибочный тип</summary>
                error      = 0,                                         /// <summary>Вызов</summary>
                call       = 1,                                         /// <summary>Возврат</summary>
                @return    = 2,                                         /// <summary>Останов дальнейшего просмотра правил без условия или с условием</summary>
                stop       = 3
            };

            /// <summary>Сопоставление строковых команд типу команды. Все команды указываются в НИЖНЕМ РЕГИСТРЕ</summary>
            public static readonly SortedList<string, TransitionType> types = new SortedList<string, TransitionType>()
            {
                { "call",   TransitionType.call    },
                // { "jump",   TransitionType.jump    },
                // { "jmp",    TransitionType.jump    },
                { "return", TransitionType.@return },
                { "ret",    TransitionType.@return },
                { "err",    TransitionType.error   },
                { "error",  TransitionType.error   },
                { "stop",   TransitionType.stop    }
            };


            /// <summary>Создание подкоманды для команды cmp</summary>
            /// <param name="command">Вышестоящая команда (transition). Сюда приходит команда по типу "call:blockName"</param>
            /// <param name="commandName">Имя вышестоящей команды</param>
            /// <param name="LineNumber">Номер строки, на которой встречена данная лексема</param>
            public Transition(Directive command, string commandName, int LineNumber): base(command, LineNumber)
            {
                // Ищем нужный тип команды: jump, return, call или error
                var index = types.IndexOfKey(commandName);
                
                if (index < 0)
                    goto commandError;

                Type = types.Values[index];
                if (Type == TransitionType.error)
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Error command occured at line {LineNumber}\r\n" + command.Parameter, "", ErrorReporting.LogTypeCode.SmallError, "trustsFile.parse");

                    return;
                }

                if (Type == TransitionType.stop && command.isNegative)
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Error command occured at line {LineNumber}\r\nStop command can not be negative (':not:' is error)", "", ErrorReporting.LogTypeCode.SmallError, "trustsFile.parse");
                    return;
                }
/*
                if (Type == TransitionType.jump && commandName == "&")
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Error command occured at line {LineNumber}\r\nJump command can not have '&' parameter", "", ErrorReporting.LogTypeCode.SmallError, "trustsFile.parse");
                    return;
                }
*/

                this.Parameter = command.Parameter;

                // command.OwnObject.logger.Log($"cmp:d[{StartIndex}:{EndIndex}", command.OwnObject.Name, ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                command.syntaxError = false;
                return;

                commandError:
                {
                    command.syntaxError = true;
                    var sb = new StringBuilder();
                    sb.AppendLine($"Command '{command.Name}' at line {LineNumber} contains incorrect parameter '{command.Parameter}' (example :command:accept:0.0)");
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
