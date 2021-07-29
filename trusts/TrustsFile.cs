using System;
using System.IO;
using System.Text;
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
                var newRoot = Parse(File.ReadAllLines(this.trustsFile.FullName, new UTF8Encoding()));
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

        // Вызывается if (!listen.trusts_domain.Compliance(domainName))
        // в файле /vinny-socks5-proxy/ListenConfiguration-connection-est.cs
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
            // Удаляем начальный символ в файле (символ BOM)
            if (TrustFileLines[0][0] == 0xFEFF)
                TrustFileLines[0] = TrustFileLines[0].Substring(startIndex: 1);

            lock (this)
            {
                var root = new TrustsObject("", null, logger);
    
                int countOfBlocks = 0;
    
                TrustsObject currentObject  = null;
                Directive      currentCommand = null;
                for (int i = 0; i < TrustFileLines.Length; i++)
                {
                    try
                    {
                        var rawLine = TrustFileLines[i];
                        var tLine   = rawLine.Trim();
                        
                        if (tLine.Length <= 0 || tLine.StartsWith("#"))
                            continue;
    
                        if (tLine.StartsWith(":") && !tLine.StartsWith("::"))
                        {
                                tLine = tLine.Substring(startIndex: 1);
                            var nLine = tLine.Split(new string[] {":"}, 2, StringSplitOptions.None);
                            if (nLine.Length != 2)
                            {
                                logger.Log($"TrustsObject.Parse error at line {i+1}. Incorrect command '{tLine}'. Correct example: ':new:Name'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                return null;
                            }
    
                            var cmd = nLine[0].Trim().ToLowerInvariant();
    
                            bool isNegative = false;
                            switch (cmd)
                            {
                                case "new":
                                        if (currentObject != null)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'new' command, but a current block is not ended. End the block with a command ':end:BlockName'\r\nNested blocks are not allowed", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
    
                                        var nameOfBlock = nLine[1].Trim();
                                        if (nameOfBlock.Length <= 0)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'new' command, but the block does not have name. Example :new:BlockName", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
    
                                        currentObject  = new TrustsObject(nameOfBlock, root);
                                        currentCommand = null;
                                        countOfBlocks++;
                                    break;
        
                                case "end":
                                        if (currentObject == null)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'end' command, but an current block is missing. Start block with command ':new:BlockName'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
                                        
                                        if (currentObject.Name != nLine[1].Trim())
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'end' command, but not have right name of ended block. Example :end:BlockName", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
    
                                        currentObject  = null;
                                        currentCommand = null;
                                    break;

                                // Копия команд в ParseSubCommand
                                case "must":
                                case "may":
                                        if (currentObject == null)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
        
                                        isNegative = false;
                                        if (nLine[1].ToLowerInvariant().StartsWith("not:"))
                                        {
                                            nLine[1]   = nLine[1].Substring(startIndex: 4);
                                            isNegative = true;
                                        }

                                        currentCommand = new Directive("compare", nLine[1], isNegative, currentObject, i+1);
        
                                        currentCommand.SubCommand = new Compare(currentCommand, i+1, cmd == "may");
        
                                        if (currentCommand.syntaxError)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. In '{cmd}' command a syntax error found", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
        
                                        // logger.Log($"Parsed command '{currentCommand.Name}' with subcommand '{currentCommand.SubCommand.ToString()}'", "", ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                                        currentObject.commands.Add(currentCommand);
                                    break;
        
                                case "command":
                                        if (currentObject == null)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
        
                                        isNegative = false;
                                        if (nLine[1].ToLowerInvariant().StartsWith("not:"))
                                        {
                                            nLine[1]   = nLine[1].Substring(startIndex: 4);
                                            isNegative = true;
                                        }
    
                                        currentCommand = new Directive("command", nLine[1], isNegative, currentObject, i+1);
        
                                        currentCommand.SubCommand = new Command(currentCommand, i+1);
        
                                        if (currentCommand.syntaxError)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. In '{cmd}' command a syntax error found", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
        
                                        // logger.Log($"Parsed command '{currentCommand.Name}' with subcommand '{currentCommand.SubCommand.ToString()}'", "", ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                                        currentObject.commands.Add(currentCommand);
                                        currentCommand = null;
                                    break;
        
                                case "ret":
                                case "return":
                                // case "jump":
                                // case "jmp":
                                case "call":
                                case "stop":
                                case "err":
                                case "error":
                                        if (currentObject == null)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
                                        
                                        isNegative = false;
                                        if (nLine[1].ToLowerInvariant().StartsWith("not:"))
                                        {
                                            nLine[1]   = nLine[1].Substring(startIndex: 4);
                                            isNegative = true;
                                        }
    
                                        currentCommand = new Directive("transition", nLine[1], isNegative, currentObject, i+1);
                                        currentCommand.SubCommand = new Transition(currentCommand, cmd, i+1);
    
                                        currentObject.commands.Add(currentCommand);
                                    break;

                                case "info":
                                        if (currentObject == null)
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }

                                        isNegative = false;
                                        if (nLine[1].ToLowerInvariant().StartsWith("not:"))
                                        {
                                            logger.Log($"TrustsObject.Parse error at line {i+1}. Info command can not be negative", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                            return null;
                                        }
    
                                        currentCommand = new Directive("info", nLine[1], isNegative, currentObject, i+1);

                                        // logger.Log($"Parsed command '{currentCommand.Name}' with subcommand '{currentCommand.SubCommand.ToString()}'", "", ErrorReporting.LogTypeCode.Usually, "trustsFile.parse");
                                        currentObject.commands.Add(currentCommand);
                                        currentCommand = null;
                                    break;
    
                                default:
                                    logger.Log($"TrustsObject.Parse error at line {i+1}. Incorrect command '{tLine}' ('{cmd}'). Correct example: ':new:Name'", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                    return null;
                            }
                        }
                        // Если линия начинается не на ":"
                        else
                        {
                            if (tLine.StartsWith("::"))
                                tLine = tLine.Substring(startIndex: 2, tLine.Length - 2);

                            if (currentObject == null || currentCommand == null)
                            {
                                logger.Log($"TrustsObject.Parse error at line {i+1}. Parameter '{tLine}' for a command encountered, but have no the command", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                return null;
                            }

                            currentCommand.SubCommand.addParameter(tLine);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Log($"Unknown TrustsObject.Parse error at line {i+1}\r\n{e.Message}\r\n{e.StackTrace}", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                        return null;
                    }
                }

                try
                {
                    if (!root.checkTransitionsParameters())
                    {
                        logger.Log($"TrustsObject.Parse: transitions names in the file is incorrect", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.FatalError, "trustsFile.parse");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    logger.Log($"Unknown TrustsObject.Parse error in the checkTransitionsParameters functions\r\n{e.Message}\r\n{e.StackTrace}", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                    return null;
                }

                logger.Log($"TrustsObject.Parse: a success end. {countOfBlocks} blocks has been parsed", trustsFile?.FullName ?? "", ErrorReporting.LogTypeCode.Changed, "trustsFile.parse.message");

                return root;
            }
        }
    }
}
