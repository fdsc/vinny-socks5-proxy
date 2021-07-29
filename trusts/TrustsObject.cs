using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
                    {
                        if (cmd.SubCommand is Compare && cmd.SubCommand.parametres.Count <= 0)
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("Example");
                            sb.AppendLine(":cmp:exactly:d[0:1]");
                            sb.AppendLine("yandex.ru");
                            sb.AppendLine("yandex.com");
                            sb.AppendLine("yandex.net");
                            logger.Log($"Error in Compare ('cmp') command at line {cmd.LineNumber}: compare must have at least one parameter (lines below command)\r\n{sb.ToString()}", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                            return false;
                        }

                        continue;
                    }

                    // Если команда является переходом
                    // Рекурсия недопустима
                    var name = cmd.Parameter;
                    if (name == block.Key)
                    {
                        logger.Log($"Error transition at line {cmd.LineNumber}: recursion not allowed {cmd.ToString()}", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                        return false;
                    }

                    if (name == "&" && transition.Type != Transition.TransitionType.error)
                    {
                        var haveError = false;
                        foreach (var param in cmd.SubCommand.parametres)
                        {
                            if (!rootCollection.ContainsKey(param))
                            {
                                haveError = true;
                                logger.Log($"Error transition at line {cmd.LineNumber}: the block for transition is not found for command '{cmd.ToString()}' and parameter '{param}'", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                            }
                        }

                        if (haveError)
                            return false;

                         continue;
                    }
                    else
                    if (rootCollection.ContainsKey(name))
                        continue;

                    logger.Log($"Error transition at line {cmd.LineNumber}: the block for transition is not found for command '{cmd.ToString()}'", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");

                    return false;
                }

                // Контроль за тем, что команды, которые не имеют смысла, никогда не будут выполнены (будет синтактическая ошибка)
                var cmds = block.Value.commands;
                if (cmds.Count > 0)
                {
                    var lastCommand = cmds[cmds.Count - 1].SubCommand as Compare;
                    if (lastCommand == null)
                        continue;

                    if (lastCommand.maybe && !lastCommand.command.isNegative)
                    {
                        logger.Log($"Error transition at line {lastCommand.LineNumber}: 'may' positive command is a last command of block. The command is meaningless", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");

                        return false;
                    }
                    else
                    if (!lastCommand.maybe && lastCommand.command.isNegative)
                    {
                        logger.Log($"Error transition at line {lastCommand.LineNumber}: 'must' negative command is a last command of block. The command is meaningless", block.Key, ErrorReporting.LogTypeCode.Error, "trustsFile.parse");

                        return false;
                    }
                }
            }

            return true;
        }
        
        /// <summary>Описывает возврат, поступивший из команды</summary>
        protected enum TrustsProgramContinuation
        {                                                       /// <summary>Ошибочный тип возврата</summary>
            error     = 0,                                      /// <summary>Возврат true</summary>
            @true     = 1,                                      /// <summary>Возврат false</summary>
            @false    = 2,                                      /// <summary>Возврат команды останова</summary>
            stop      = 3
        }

        /// <summary>Определяет, соответствует ли политике доменных имён данная строка</summary>
        /// <returns>True, если имя соответствует политике</returns>
        /// <param name="domainName">Проверяемое доменное имя</param>
        public bool Compliance(string domainName)
        {
            var Name        = new DomainName(domainName);
            var commandType = Command.CommandType.reject;
            var priority    = new Priority(null);
            var root        = this.rootCollection["root"];

            if (Name.syntaxError)
                return false;

            Complains(Name, ref commandType, ref priority, root);

            return commandType == Command.CommandType.accept;
        }

        private TrustsProgramContinuation Complains(DomainName domainName, ref Command.CommandType commandType, ref Priority priority, TrustsObject root)
        {
            foreach (var cmd in root.commands)
            {
                var returnTypeTrue  = cmd.isNegative ? TrustsProgramContinuation.@false : TrustsProgramContinuation.@true;
                var returnTypeFalse = cmd.isNegative ? TrustsProgramContinuation.@true  : TrustsProgramContinuation.@false;

                if (cmd.SubCommand is Compare)
                {
                    var cmp = cmd.SubCommand as Compare;
                    var cmpResult = Compliance(domainName, cmp);
                    if (!cmpResult && !cmp.maybe)
                        return returnTypeFalse;
                    else
                    if (cmpResult && cmp.maybe)
                        return returnTypeTrue;
                }
                else
                if (cmd.SubCommand is Transition)
                {
                    var trn = cmd.SubCommand as Transition;

/*
    call выполняет блоки из параметров до тех пор, пока один из них не вернёт true
        Если никто не вернул true, завершает выполнение текущего блока с возвращаемым результатом false

    stop выполняет блоки до тех пор, пока один из блоков не вернёт true. Тогда возвращает вверх коамнду stop (останов проверок)

    return выполняет блоки до тех пор, пока один из них не вернёт true.
        Если никто не вернул true, то продолжает работу блока. В противном случае, возвращает true
*/
                    if (trn.Type == Transition.TransitionType.error)
                    {
                        logger.Log($"Error occured: {trn.Parameter}", "", ErrorReporting.LogTypeCode.SmallError, "trustsFile.check.errorCommand");
                        return TrustsProgramContinuation.stop;
                    }

                    if (trn.Parameter == "&")
                    {
                        bool isCallTrue = false;
                        foreach (var blockName in trn.parametres)
                        {
                            // Делаем переход на другой блок
                            var result = Complains(domainName, ref commandType, ref priority, root.rootCollection[blockName]);

                            // Если внутри обрабатываемого блока сработала команда stop, передаём её выше
                            if (result == TrustsProgramContinuation.stop)
                                return TrustsProgramContinuation.stop;

                            // Если выполнение в команде stop возвратило true, передаём команду stop выше
                            if (trn.Type == Transition.TransitionType.stop && result == TrustsProgramContinuation.@true)
                                return TrustsProgramContinuation.stop;

                            // Если вызов call вернул true, то мы дальше продолжаем выполнять блок,
                            // но call в этой команде уже не вызываем
                            if (trn.Type == Transition.TransitionType.call && result == TrustsProgramContinuation.@true)
                            {
                                isCallTrue = true;
                                break;
                            }

                            // Если вызов return, то он возвращает значение, только если результат выполнения блока - true
                            if (trn.Type == Transition.TransitionType.@return && result == TrustsProgramContinuation.@true)
                            {
                                return returnTypeTrue;
                            }
                        }

                        // Если мы, исполняя call, не нашли ни одного блока, который бы выдал true, значит мы уходим из функции
                        if (trn.Type == Transition.TransitionType.call && !isCallTrue)
                            return returnTypeFalse;
                    }
                    else
                    {
                        // Делаем переход на другой блок
                        var result = Complains(domainName, ref commandType, ref priority, root.rootCollection[trn.Parameter]);

                        // Если внутри обрабатываемого блока сработала команда stop, передаём её выше
                        if (result == TrustsProgramContinuation.stop)
                            return TrustsProgramContinuation.stop;

                        // Если выполнение в команде stop возвратило true, передаём команду stop выше
                        if (trn.Type == Transition.TransitionType.stop && result == TrustsProgramContinuation.@true)
                            return TrustsProgramContinuation.stop;

                        // Если вызов call вернул false, то прерываем выполнение данного блока
                        if (trn.Type == Transition.TransitionType.call && result == TrustsProgramContinuation.@false)
                            return returnTypeFalse;

                        // Если вызов return, то он возвращает значение, только если результат выполнения блока - true
                        if (trn.Type == Transition.TransitionType.@return && result == TrustsProgramContinuation.@true)
                            return returnTypeTrue;
                    }
                }
                else
                if (cmd.SubCommand is Command)
                {
                    // Если это команда (accept, reject),
                    // то проверяем приоритет
                    // Если приоритет выше, устанавливаем новую команду
                    var command = cmd.SubCommand as Command;
                    if (command.Priority > priority)
                    {
                        priority = command.Priority;
                        commandType = command.Type;
                    }
                }
                else
                {
                    throw new Exception();
                }
            }

            // Если дошли до конца блока, то возвращаем true
            return TrustsProgramContinuation.@true;
        }

        /// <summary>Проверка на соответствие команды Compare</summary>
        /// <returns>True, если домен соответствует правилу (найдены совпадения)</returns>
        /// <param name="domainName">Домен для проверки</param>
        /// <param name="compare">Команда, на соответствие которой проверяется домен</param>
        public bool Compliance(DomainName domainName, Compare compare)
        {
            var domain = domainName[compare.StartIndex, compare.EndIndex];
            var dsplit = compare.splitRegime == Compare.SplitRegime.splitted ? domainName.Splitted(compare.StartIndex, compare.EndIndex) : null;

            switch (compare.Type)
            {
                case Compare.CompareType.exactly:

                        if (compare.splitRegime == Compare.SplitRegime.inString)
                        {
                            foreach (var param in compare.parametres)
                                if (domain == param)
                                    return true;
                        }
                        else
                        {
                            foreach (var param in compare.parametres)
                            foreach (var sub   in dsplit)
                                if (sub == param)
                                    return true;
                        }
                    break;
                    
                case Compare.CompareType.contains:

                        if (compare.splitRegime == Compare.SplitRegime.inString)
                        {
                            foreach (var param in compare.parametres)
                                if (domain.Contains(param))
                                    return true;
                        }
                        else
                        {
                            foreach (var param in compare.parametres)
                            foreach (var sub   in dsplit)
                                if (sub.Contains(param))
                                    return true;
                        }
                    break;

                case Compare.CompareType.endsWith:

                        if (compare.splitRegime == Compare.SplitRegime.inString)
                        {
                            foreach (var param in compare.parametres)
                                if (domain.EndsWith(param))
                                    return true;
                        }
                        else
                        {
                            foreach (var param in compare.parametres)
                            foreach (var sub   in dsplit)
                                if (sub.EndsWith(param))
                                    return true;
                        }
                    break;

                case Compare.CompareType.startsWith:

                        if (compare.splitRegime == Compare.SplitRegime.inString)
                        {
                            foreach (var param in compare.parametres)
                                if (domain.StartsWith(param))
                                    return true;
                        }
                        else
                        {
                            foreach (var param in compare.parametres)
                            foreach (var sub   in dsplit)
                                if (sub.StartsWith(param))
                                    return true;
                        }
                    break;

                case Compare.CompareType.regex:

                        if (compare.splitRegime == Compare.SplitRegime.inString)
                        {
                            foreach (var param in compare.parametres)
                            {
                                var regex = new Regex(param, RegexOptions.IgnoreCase);
                                if (regex.IsMatch(domain))
                                    return true;
                            }
                        }
                        else
                        {
                            foreach (var param in compare.parametres)
                            {
                                var regex = new Regex(param, RegexOptions.IgnoreCase);
                                foreach (var sub in dsplit)
                                    if (regex.IsMatch(sub))
                                    return true;
                            }
                        }
                    break;

                default:
                    throw new Exception($"Unknown type of compare command {compare.Type}");
            }

            // Совпадений не найдено
            return false;
        }
    }
}
