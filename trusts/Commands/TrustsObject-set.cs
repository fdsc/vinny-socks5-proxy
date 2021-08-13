using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Подкоманда команды cmp. Тип команды задаётся полем типа CompareType</summary>
        public class SetCommand: SubCommand
        {                                                               /// <summary>Тип команды set</summary>
            public readonly SetType Type = 0;
                                                                        /// <summary>Тип команды set</summary>
            // При изменении типа добавить ниже, в переменную types, перечень допустимых параметров
            public enum SetType
            {                                                           /// <summary>Ошибочный тип</summary>
                error      = 0,                                         /// <summary>Группа логирования доменов</summary>
                logDomain  = 1
            };

            /// <summary>Сопоставление строковых команд целочисленному типу команды. Все команды указываются в НИЖНЕМ РЕГИСТРЕ</summary>
            public static readonly SortedList<string, SetType> types = new SortedList<string, SetType>()
            {
                { "logDomain", SetType.logDomain }
            };


            /// <summary>Создание подкоманды для команды set</summary>
            /// <param name="command">Вышестоящая команда (cmp). Сюда приходит команда по типу "exactly:d[0:1]"</param>
            /// <param name="LineNumber">Номер строки, на которой встречена данная лексема</param>
            public SetCommand(Directive command, int LineNumber): base(command, LineNumber)
            {
                // Здесь строка, идущая после set:
                var splitted = command.Parameter.Split(new string[] {":"}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length < 2)
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Command '{command.Name}' at line {LineNumber} contains incorrect parameter '{command.Parameter}' (example: set:logDomain:new:NameOfLogGroup:FileNameOfGroup)", command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
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
                    sb.AppendLine($"Command '{command.Name}' at line {LineNumber} contains incorrect parameter '{command.Parameter}' (example :cmp:exactly:d[:])");
                    sb.AppendLine("List of correct parameters name:");
                    foreach (var type in types)
                        sb.AppendLine(type.Key);

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
                else
                    Type = types.Values[index];
            }

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
                    case SetType.logDomain:
                        break;
                    default:
                        command.syntaxError = true;
                        return;
                }
            }
        }
    }
}
