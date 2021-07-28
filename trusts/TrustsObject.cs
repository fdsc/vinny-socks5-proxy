using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using trusts.Commands;

namespace trusts
{
    /// <summary>Объект иерархии доверия</summary>
    public partial class TrustsObject
    {                                                               /// <summary>Корень настроек. Это один объект на всех, который хранит полный список объектов в rootCollection</summary>
        public readonly TrustsObject   root;                        /// <summary>Уникальное имя объекта</summary>
        public readonly string         Name   = null;               /// <summary>Объект, определяющий политику логирования. Один на всю иерархию</summary>
        public readonly ErrorReporting logger = null;

        /// <summary>Полная коллекция объектов иерархии. Это коллекция блоков</summary>
        public readonly ConcurrentDictionary<string, TrustsObject> rootCollection = null;           /// <summary>Команды блока. У корневого блока команд нет</summary>
        public readonly List<Directive> commands = new List<Directive>();

        /// <summary>Создаёт объект иерархии настроек. Добавляет этот объект в rootCollection</summary>
        /// <param name="Name">Уникальное для иерархии имя объекта</param>
        /// <param name="root">Корневой объект иерархии</param>
        /// <param name="logger">Объект, определяющий политику логирования (может быть null)</param>
        public TrustsObject(string Name, TrustsObject root, ErrorReporting logger = null)
        {
            if (Name == null)
                throw new ArgumentNullException();

            this.Name = Name;
            this.root = root;

            if (root == null)
            {
                this.root      = this;
                this.logger    = logger ?? new ErrorReporting();
                rootCollection = new ConcurrentDictionary<string, TrustsObject>();
            }
            else
            {
                if (logger != null)
                {
                    logger.Log($"TrustsObject constructor: logger must be just one for TrustsObject hierarchy. Object name {Name}", "", ErrorReporting.LogTypeCode.Error, "trustsFile.TrustsObject;logging.trustsFile.TrustsObject");
                    this.root?.logger?.Log($"TrustsObject constructor: logger must be just one for TrustsObject hierarchy. Object name {Name}", "", ErrorReporting.LogTypeCode.Error, "trustsFile.TrustsObject;logging.trustsFile.TrustsObject");
                    throw new ArgumentException();
                }

                this.logger    = this.root.logger;
                rootCollection = this.root.rootCollection;
            }

            if (!this.root.rootCollection.TryAdd(this.Name, this))
            {
                this.logger.Log($"TrustsObject constructor: name of object not unique: {Name}", "", ErrorReporting.LogTypeCode.Error, "trustsFile.TrustsObject;trustsFile.parse");
                throw new ArgumentException();
            }
        }

        /// <summary>Определяет, соответствует ли политике доменных имён данная строка</summary>
        /// <returns>True, если имя соответствует политике</returns>
        /// <param name="domainName">Проверяемое доменное имя</param>
        public bool Compliance(string domainName)
        {
            var commandType = Command.CommandType.reject;
            var priority    = new Priority(null);

            

            return true; // TODO: Ничего не сделано
        }

        /// <summary>Производит проверку того, что в переходах всегда указаны верные имена блоков для перехода</summary>
        /// <returns><c>true</c>, если файл имеет корректные имена переходов, <c>false</c> если файл некорректен.</returns>
        public bool checkTransitionsParameters()
        {
            if (!rootCollection.ContainsKey("root"))
            {
                logger.Log($"Error transition: trusts file must contains 'root' block - the program only checks it\r\nThe remaining blocks are checked only if there is a transition to them", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                return false;
            }

            // Проходим по всем блокам
            foreach (var block in rootCollection)
            {
                // Проходим по всем командам блоков
                foreach (var cmd in block.Value.commands)
                {
                    var transition = cmd.SubCommand as Transition;
                    if (transition == null || transition.Type == Transition.TransitionType.error || transition.Type == Transition.TransitionType.@return)
                        continue;

                    // Рекурсия недопустима
                    var name = cmd.Parameter;
                    if (name == block.Key)
                    {
                        logger.Log($"Error transition: recursion not allowed {cmd.ToString()}", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                        return false;
                    }

                    if (rootCollection.ContainsKey(name))
                        continue;

                    logger.Log($"Error transition: the block for transition is not found for command {cmd.ToString()}", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");

                    return false;
                }
            }

            return true;
        }
    }
}
