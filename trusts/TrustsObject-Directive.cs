using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace trusts
{
    public partial class TrustsObject
    {
        /// <summary>Класс представляет команду в блоке (например, cmp, return, command)</summary>
        public class Directive
        {                                                                /// <summary>Имя команды (cmp, command, transition)</summary>
            public readonly string Name        = null;                   /// <summary>Параметр команды</summary>
            public readonly string Parameter   = null;                   /// <summary>Если true, то в команде есть отрицание</summary>
            public readonly bool   isNegative  = false;                  /// <summary>Если true, то при разборе команды была обнаружена синтаксическая ошибка. После работы конструктора значение всегда true (оно сбрасывается потом при обработке SubCommand)</summary>
            public          bool   syntaxError = true;
                                                                         /// <summary>Строка, на которой декларирована команда. Нумерация с единицы</summary>
            public readonly int    LineNumber  = -1;
            
                                                                         /// <summary>Подкоманда (например, exactly)</summary>
            public          SubCommand   SubCommand = null;
                                                                        /// <summary>Блок, владеющий данной командой</summary>
            public readonly TrustsObject OwnObject  = null;

            /// <summary>Создаёт экземпляр команды</summary><param name="Name">Имя команды (cmp, return, ...)</param>
            /// <param name="Parameter">Строка, идущая после ":", следующего за именем команды, кроме оператора ":not"</param>
            /// <param name="isNegative">Наличие оператора ":not". Если true, то возвращаемое значение команды будет инвертироваться</param>
            /// <param name="own">Блок, включающий в себя данную команду</param>
            /// <param name="LineNumber">Номер строки, на которой встречена данная лексема</param>
            public Directive(string Name, string Parameter, bool isNegative, TrustsObject own, int LineNumber)
            {
                this.Name       = Name;
                this.Parameter  = Parameter;
                this.isNegative = isNegative;
                this.OwnObject  = own;
                this.LineNumber = LineNumber;
            }

            /// <summary>Возвращает представление команды</summary>
            /// <returns>Строковое представление команды для отладки. Для любых переходов даёт transition вместо типа перехода</returns>
            public override string ToString()
            {
                if (isNegative)
                {
                    return Name +  ":not:" + Parameter;
                }
                else
                    return Name +  ":" + Parameter;
            }
        }

        /// <summary>Представляет собой описание подкоманды. Для команды cmp это подкоманды exactly и т.п.</summary>
        public class SubCommand
        {                                                                   /// <summary>Вышестоящая команда</summary>
            public readonly Directive    command;                           /// <summary>Параметры команды, которые идут на следующих под ней строках</summary>
            public readonly List<string> parametres = new List<string>();   /// <summary>Строка, на которой декларирована команда. Нумерация с единицы</summary>
            public readonly int    LineNumber  = -1;

                                                                        /// <summary>Базовый конструктор</summary>
            protected SubCommand(Directive command, int LineNumber)
            {
                this.command    = command;
                this.LineNumber = LineNumber;
            }
                                                                        /// <summary>Имя подкоманды</summary>
            public virtual string Name => "";                           /// <summary>Преобразование команды в текстовый вид</summary>
            public override string ToString()
            {
                return Name;
            }

            /// <summary>Добавляет параметр команды (отдельная строка, идущая ниже команды)</summary>
            /// <param name="tLine">Строка параметра команды</param>
            public virtual void addParameter(string tLine)
            {
                parametres.Add(tLine.Trim());
            }
        }
    }
}
