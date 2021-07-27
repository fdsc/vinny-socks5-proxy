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
    }
}
