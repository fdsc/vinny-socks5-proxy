using System;
using System.IO;
using static trusts.TrustsObject;

namespace trusts
{
    /// <summary>Представляет trusts-файл и отслеживает его изменения</summary>
    public class TrustsFile
    {                                                                           /// <summary>Исходный файл настроек. <see langword="null"/>, если настройки загружены не из файла (isFromFile == false)</summary>
        public readonly FileInfo          trustsFile        = null;             /// <summary>Осуществляет отслеживание изменений файла</summary>
        public readonly FileSystemWatcher trustsFileWatcher = null;             /// <summary>Определяет политику логирования</summary>
        public readonly ErrorReporting    logger            = null;             /// <summary>Если true, то используется файл, иначе информация была загружена из другого источника</summary>
        public readonly bool              isFromFile        = false;            /// <summary>Корень настроек</summary>
        public volatile TrustsObject      root              = null;
                                                                                        /// <summary>Время последнего доступа к файлу настроек (используется для отслеживания изменений)</summary>
        public          DateTime          LastWriteTimeToTrustsFile = default;

        /// <summary>Базовый конструктор</summary>
        /// <param name="logger">Принимает наследника ErrorReporting (может быть <see langword="null"/>). Определяет политику логирования</param>
        public TrustsFile(ErrorReporting logger)
        {
            this.logger = logger ?? new ErrorReporting();
        }

        /// <summary>Конструтор для создания объекта, привязанного к файлу на дике</summary>
        /// <param name="trustsFile">Имя файла на диске</param>
        /// <param name="logger">Аналогично базовому конструктору (определяет политику логирования)</param>
        public TrustsFile(string trustsFile, ErrorReporting logger): this(logger)
        {
            isFromFile      = true;
            this.trustsFile = new FileInfo(trustsFile);

            if (!this.trustsFile.Exists)
            {
                logger.Log($"Trusts file not exists: {this.trustsFile.FullName}", "", ErrorReporting.LogTypeCode.FatalError, "logging.trustsFile;trustsFile;filesystem");
                throw new FileNotFoundException();
            }

            SetLastTimeTrustsWrite();

            trustsFileWatcher = new FileSystemWatcher(this.trustsFile.DirectoryName, this.trustsFile.Name);   // За отдельными файлами он следить не умеет
            trustsFileWatcher.Changed += TrustsFileWatcher_Changed;
            trustsFileWatcher.Renamed += TrustsFileWatcher_Changed;
            trustsFileWatcher.EnableRaisingEvents = true;
            trustsFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;

            var newRoot = Parse(  File.ReadAllLines(this.trustsFile.FullName)  );
            if (newRoot == null)
            {
                logger.Log($"Trusts file is incorrect: {this.trustsFile.FullName}", "", ErrorReporting.LogTypeCode.FatalError, "trustsFile");
                throw new ArgumentException();
            }
            else
                SetNewRoot(newRoot);
        }

        /// <summary>Конструктор, создающий объект без привязки к конкретному файлу</summary>
        /// <param name="TrustFileLines">Строки настройки</param>
        /// <param name="logger">Аналогично базовому конструктору (определяет политику логирования)</param>
        public TrustsFile(string[] TrustFileLines, ErrorReporting logger): this(logger)
        {
            SetNewRoot(  Parse(TrustFileLines)  );
        }

        /// <summary>Заменяет старую иерархию новой иерархией объектов</summary>
        /// <returns><c>true</c>, if new root was set, <c>false</c> otherwise.</returns>
        /// <param name="newRoot">Корень новой иерархии объектов, замещающей старую</param>
        public bool SetNewRoot(TrustsObject newRoot)
        {
            if (newRoot != null)
            {
                root = newRoot;

                return true;
            }

            return false;
        }

        /// <summary>Обработчик изменений отслеживаемого файла настроек</summary><param name="sender"></param><param name="e"></param>
        protected void TrustsFileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (isFromFile)
            lock (this)
            {
                trustsFile.Refresh();
                if (LastWriteTimeToTrustsFile == trustsFile.LastWriteTimeUtc)
                    return;

                SetLastTimeTrustsWrite();
                logger.Log("Trust file changed " + trustsFile.LastWriteTime, trustsFile.FullName, ErrorReporting.LogTypeCode.Usually, "trustsFile");
                var newRoot = Parse(File.ReadAllLines(this.trustsFile.FullName));
                if (newRoot == null)
                {
                    logger.Log($"Trusts file is incorrect: {this.trustsFile.FullName}\r\nServer still work with previous trusts configuration", "", ErrorReporting.LogTypeCode.FatalError, "trustsFile");
                    return;
                }
                else
                    SetNewRoot(newRoot);
            }
        }

        private void SetLastTimeTrustsWrite()
        {
            LastWriteTimeToTrustsFile = trustsFile.LastWriteTimeUtc;
        }

        /// <summary>Определяет, соответствует ли политике доменных имён данная строка</summary>
        /// <returns>True, если имя соответствует политике</returns>
        /// <param name="domainName">Проверяемое доменное имя</param>
        public bool Compliance(string domainName)
        {
            lock (this)
            {
                return root.Compliance(domainName);
            }
        }

        /// <summary>Осуществляет парсинг содержимого файла (или строк без файла)</summary>
        /// <returns>null, если возникла ошибка</returns>
        /// <param name="TrustFileLines">Строки для парсинга</param>
        public TrustsObject Parse(string[] TrustFileLines)
        {
            lock (this)
            {
                var root = new TrustsObject("", null, logger);
    
                int countOfBlocks = 0;
    
                TrustsObject currentObject  = null;
                Directive      currentCommand = null;
                for (int i = 0; i < TrustFileLines.Length; i++)
                {
                    var rawLine = TrustFileLines[i];
                    var tLine   = rawLine.Trim();
                    
                    if (tLine.Length <= 0 || tLine.StartsWith("#"))
                        continue;
                    
                    if (tLine.StartsWith(":"))
                    {
                        var nLine = tLine.Substring(startIndex: 1).Split(new string[] {":"}, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (nLine.Length != 2)
                        {
                            logger.Log($"TrustsObject.Parse error at line {i+1}. Incorrect command '{tLine}'. Correct example: ':new:Name'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                            return null;
                        }
    
                        var cmd = nLine[0].Trim().ToLowerInvariant();
                        if (cmd.StartsWith("::", StringComparison.InvariantCulture))       // Смотрим на экранирующий двоеточие символ
                            cmd = cmd.Substring(startIndex: 2).Trim();
    
                        bool isNegative = false;
                        switch (cmd)
                        {
                            case "new":
                                    if (currentObject != null)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'new' command, but a current block is not ended. End the block with a command ':end:BlockName'\r\nNested blocks are not allowed", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }
    
                                    countOfBlocks++;
                                    currentObject  = new TrustsObject(nLine[1].Trim(), root);
                                    currentCommand = null;
                                break;
    
                            case "end":
                                    if (currentObject == null)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'end' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }
    
                                    currentObject  = null;
                                    currentCommand = null;
                                break;
    
                            // Копия команд в ParseSubCommand
                            case "cmp":
                            case "compare":
                                    if (currentObject == null)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }
    
                                    isNegative = false;
                                    if (nLine[1].ToLowerInvariant().StartsWith("not:"))
                                    {
                                        nLine[1]   = nLine[1].Substring(startIndex: 4);
                                        isNegative = true;
                                    }
    
                                    currentCommand = new Directive("compare", nLine[1], isNegative, currentObject);
    
                                    currentCommand.SubCommand = new Compare(currentCommand);
    
                                    if (currentCommand.syntaxError)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. In '{cmd}' command a syntax error found", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }
    
                                    // logger.Log($"Parsed command '{currentCommand.Name}' with subcommand '{currentCommand.SubCommand.ToString()}'", "", ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                                    currentObject.commands.Add(currentCommand);
                                break;
    
                            case "command":
                                    if (currentObject == null)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }
    
                                    isNegative = false;
                                    if (nLine[1].ToLowerInvariant().StartsWith("not:"))
                                    {
                                        nLine[1]   = nLine[1].Substring(startIndex: 4);
                                        isNegative = true;
                                    }
                                    
                                    currentCommand = new Directive("command", nLine[1], isNegative, currentObject);
    
                                    currentCommand.SubCommand = new Command(currentCommand);
    
                                    if (currentCommand.syntaxError)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. In '{cmd}' command a syntax error found", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }
    
                                    // logger.Log($"Parsed command '{currentCommand.Name}' with subcommand '{currentCommand.SubCommand.ToString()}'", "", ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                                    currentObject.commands.Add(currentCommand);
                                break;
    
                            case "ret":
                            case "return":
                            case "jump":
                            case "jmp":
                            case "call":
                            case "err":
                            case "error":
                                    if (currentObject == null)
                                    {
                                        logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                        return null;
                                    }

                                    currentCommand = new Directive("transition", nLine[1], isNegative, currentObject);
                                    currentCommand.SubCommand = new Transition(currentCommand, cmd);

                                    currentObject.commands.Add(currentCommand);
                                    currentCommand = null;
                                break;

                            default:
                                logger.Log($"TrustsObject.Parse error at line {i+1}. Incorrect command '{tLine}' ('{cmd}'). Correct example: ':new:Name'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                return null;
                        }
                    }
                }

                if (!root.checkTransitionsParameters())
                {
                    logger.Log($"TrustsObject.Parse: transitions names in the file is incorrect", trustsFile.FullName, ErrorReporting.LogTypeCode.FatalError, "trustsFile.parse");
                    return null;
                }

                logger.Log($"TrustsObject.Parse: a success end. {countOfBlocks} blocks has been parsed", trustsFile.FullName, ErrorReporting.LogTypeCode.Changed, "trustsFile.parse.message");

                return root;
            }
        }
    }
}
