using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Команда, заставляющая после вызова callback ждать некоторое время</summary>
        public class SleepCommand: SubCommand
        {                                                               /// <summary>Сколько нужно ждать микросекунд из расчёта на один принятый байт</summary>
            public readonly Int64 SleepInterval = 0;

            /// <summary>Команда, заставляющая после вызова callback ждать некоторое время</summary>
            /// <param name="command">Вышестоящая команда (cmp). Сюда приходит команда по типу "sleep:1"</param>
            /// <param name="LineNumber">Номер строки, на которой встречена данная лексема</param>
            public SleepCommand(Directive command, int LineNumber): base(command, LineNumber)
            {
                command.syntaxError = true;
                
                // command.OwnObject.logger.Log("SleepCommand " + command.Parameter, "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");

                // Здесь строка, идущая после sleep:
                if (!Int64.TryParse(command.Parameter, out SleepInterval))
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Command '{command.Name}' at line {LineNumber} contains incorrect parameter '{command.Parameter}' (example: :sleep:10)", command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }

                command.syntaxError = false;
            }
        }
    }
}
