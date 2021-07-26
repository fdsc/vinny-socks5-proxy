using System;
using System.IO;

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

            LastWriteTimeToTrustsFile = this.trustsFile.LastWriteTimeUtc;

            trustsFileWatcher = new FileSystemWatcher(this.trustsFile.DirectoryName);   // За отдельными файлами он следить не умеет
            trustsFileWatcher.Changed += TrustsFileWatcher_Changed;

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

        /// <summary>Осуществляет парсинг содержимого файла (или строк без файла)</summary>
        /// <returns>null, если возникла ошибка</returns>
        /// <param name="TrustFileLines">Строки для парсинга</param>
        public TrustsObject Parse(string[] TrustFileLines)
        {
            var root = new TrustsObject("", null, logger);
            
            int countOfBlocks = 0;

            TrustsObject currentObject  = null;
            TrustsObject currentCommand = null;
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
                    switch (cmd)
                    {
                        case "new":
                                if (currentObject != null)
                                {
                                    logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'new' command, but a current block is not ended. End the block with a command ':end:BlockName'\r\nNested blocks are not allowed", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                    return null;
                                }

                                countOfBlocks++;
                                currentObject = new TrustsObject(nLine[1].Trim(), root);
                                

                            break;

                        case "end":
                                if (currentObject == null)
                                {
                                    logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered 'end' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                    return null;
                                }

                                currentObject = null;
                            break;

                        case "cmp":
                        case "compare":
                                if (currentObject == null)
                                {
                                    logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                    return null;
                                }
                            break;

                        case "command":
                                if (currentObject == null)
                                {
                                    logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                    return null;
                                }
                            break;

                        case "call":
                                if (currentObject == null)
                                {
                                    logger.Log($"TrustsObject.Parse error at line {i+1}. Encountered '{cmd}' command, but an current block is missing. Start block with command ':new:BlockName'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                                    return null;
                                }
                            break;

                        default:
                            logger.Log($"TrustsObject.Parse error at line {i+1}. Incorrect command '{tLine}' ('{cmd}'). Correct example: ':new:Name'", "", ErrorReporting.LogTypeCode.Error, "trustsFile.parse");
                            return null;
                    }
                }
            }

            logger.Log($"TrustsObject.Parse: a success end. {countOfBlocks} blocks has been parsed", "", ErrorReporting.LogTypeCode.Usually, "trustsFile.parse.message");

            return root;
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
            {
                if (LastWriteTimeToTrustsFile == this.trustsFile.LastWriteTimeUtc)
                    return;

                var newRoot = Parse(  File.ReadAllLines(this.trustsFile.FullName)  );
                if (newRoot == null)
                {
                    logger.Log($"Trusts file is incorrect: {this.trustsFile.FullName}", "", ErrorReporting.LogTypeCode.FatalError, "trustsFile");
                    return;
                }
                else
                    SetNewRoot(newRoot);
            }
        }

        /// <summary>Определяет, соответствует ли политике доменных имён данная строка</summary>
        /// <returns>True, если имя соответствует политике</returns>
        /// <param name="domainName">Проверяемое доменное имя</param>
        public bool Compliance(string domainName)
        {
            return root.Compliance(domainName);
        }
    }
}
