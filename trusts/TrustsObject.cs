using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace trusts
{
    /// <summary>Объект иерархии доверия</summary>
    public class TrustsObject
    {                                                               /// <summary>Корень настроек. Это один объект на всех, который хранит полный список объектов в rootCollection</summary>
        public readonly TrustsObject   root;                        /// <summary>Уникальное имя объекта</summary>
        public readonly string         Name   = null;               /// <summary>Объект, определяющий политику логирования. Один на всю иерархию</summary>
        public readonly ErrorReporting logger = null;
                                                                                                                                /// <summary>Потомки этого объекта</summary>
        public readonly BlockingCollection  <string>               childs         = new BlockingCollection<string>();           /// <summary>Полная коллекция объектов иерархии</summary>
        public readonly ConcurrentDictionary<string, TrustsObject> rootCollection = null;

        /// <summary>Создаёт объект иерархии настроек</summary>
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
                this.logger.Log($"TrustsObject constructor: name of object not unique: {Name}", "", ErrorReporting.LogTypeCode.Error, "trustsFile.TrustsObject");
                throw new ArgumentException();
            }
        }

        /// <summary>Определяет, соответствует ли политике доменных имён данная строка</summary>
        /// <returns>True, если имя соответствует политике</returns>
        /// <param name="domainName">Проверяемое доменное имя</param>
        public bool Compliance(string domainName)
        {
            return true; // TODO: Ничего не сделано
        }
    }
}
