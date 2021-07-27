using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Класс представляет команду в блоке (например, cmp, return, command)</summary>
        public class Command
        {                                                                /// <summary>Имя команды</summary>
            public readonly string Name        = null;                   /// <summary>Параметр команды</summary>
            public readonly string Parameter   = null;                   /// <summary>Если true, то в команде есть отрицание</summary>
            public readonly bool   isNegative  = false;                  /// <summary>Если true, то при разборе команды была обнаружена синтаксическая ошибка</summary>
            public          bool   syntaxError = false;
                                                                         /// <summary>Подкоманда (например, exactly)</summary>
            public          SubCommand   SubCommand = null;
                                                                        /// <summary>Блок, владеющий данной командой</summary>
            public readonly TrustsObject OwnObject  = null;
            
            /// <summary>Создаёт экземпляр команды</summary><param name="Name">Имя команды (cmp, return, ...)</param>
            /// <param name="Parameter">Строка, идущая после ":", следующего за именем команды, кроме оператора ":not"</param>
            /// <param name="isNegative">Наличие оператора ":not". Если true, то возвращаемое значение команды будет инвертироваться</param>
            /// <param name="own">Блок, включающий в себя данную команду</param>
            public Command(string Name, string Parameter, bool isNegative, TrustsObject own)
            {
                this.Name       = Name;
                this.Parameter  = Parameter;
                this.isNegative = isNegative;
                this.OwnObject  = own;
            }
        }

        /// <summary>Представляет собой описание подкоманды. Для команды cmp это подкоманды exactly и т.п.</summary>
        public class SubCommand
        {                                                               /// <summary>Вышестоящая команда</summary>
            public readonly Command command;                            
                                                                        /// <summary>Базовый конструктор</summary>
            protected SubCommand(Command command)
            {
                this.command = command;
            }
                                                                        /// <summary>Имя подкоманды</summary>
            public virtual string Name => "";                           /// <summary>Преобразование команды в текстовый вид</summary>
            public override string ToString()
            {
                return Name;
            }
        }

        /// <summary>Подкоманда команды cmp. Тип команды задаётся полем типа CompareType</summary>
        public class Compare: SubCommand
        {                                                               /// <summary>Имя типа сравнения (exactly, StartsWith, ...)</summary>
            public readonly CompareType Type = 0;
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

            public static readonly SortedList<string, CompareType> types = new SortedList<string, CompareType>()
            {
                { "exactly",    CompareType.exactly },
                { "startsWith", CompareType.startsWith },
                { "endsWith",   CompareType.endsWith },
                { "contains",   CompareType.contains },
                { "regex",      CompareType.regex }
            };

            /// <summary>Создание подкоманды для команды cmp</summary>
            /// <param name="command">Вышестоящая команда (cmp)</param>
            public Compare(Command command): base(command)
            {
                var splitted = command.Parameter.Split(new string[] {":"}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (splitted.Length != 2)
                {
                    command.syntaxError = true;
                    command.OwnObject.logger.Log($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :cmp:exactly:d[:])", command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return;
                }
                
                var Name = splitted[0].Trim().ToLowerInvariant();
                
                foreach (var type in types)
                {
                    if (type.Key == Name)
                    {
                        Type = type.Value;
                        goto find;
                    }
                }

                #region Обработка ошибки, если не найдено
                {
                    command.syntaxError = true;
                    var sb = new StringBuilder();
                    sb.AppendLine($"Command '{command.Name}' contains incorrect parameter '{command.Parameter}' (example :cmp:exactly:d[:])");

                    command.OwnObject.logger.Log(sb.ToString(), command.OwnObject.Name, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                }
                #endregion

                find:
                ;
            }
                                                                        /// <summary>Имя подкоманды</summary>
            public override string Name => Type.ToString();
        }
    }
}
