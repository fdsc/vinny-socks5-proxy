using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using static trusts.Helper;

namespace trusts
{
    /// <summary>Базовый класс, определяющий способ логирования ошибок</summary>
    public class ErrorReporting_SimpleFile: ErrorReporting
    {
        /// <summary>Файл для логирования</summary>
        protected volatile FileInfo logFile = null;             /// <summary>Полное имя файла для логирования или null, если файл не определён</summary>
        public string LogFileName => logFile?.FullName;

        /// <summary>Простое создание объекта без инициализации. Вызовете SetLogFileName, иначе никакого логирования не будет вообще</summary>
        public ErrorReporting_SimpleFile()
        {
        }

        /// <summary>Базовый конструктор</summary>
        /// <param name="FileName">Имя файла для логирования</param>
        public ErrorReporting_SimpleFile(string FileName)
        {
            SetLogFileName(FileName);
        }

        /// <summary>Устанавливает имя лог-файла</summary>
        /// <param name="FileName">Имя лог-файла</param>
        public void SetLogFileName(string FileName)
        {
            logFile = new FileInfo(FileName);

            if (!logFile.Exists)
            {
                File.WriteAllText(logFile.FullName, "");
            }
        }

        /// <summary>Кодировка, используемая для логирования</summary>
        protected Encoding utf8 = new UTF8Encoding();

        /// <summary>Логирует сообщение (в базовом классе ничего не делает)</summary>
        /// <param name="Message">Сообщение для логирования</param>
        /// <param name="secondIndentification">Дополнительное идентификационное сообщение</param>
        /// <param name="isError">Общий тип сообщения об ошибке</param>
        /// <param name="messageTypeName">Тип сообщения об ошибке</param>
        public override void Log(string Message, string secondIndentification, LogTypeCode isError = LogTypeCode.Usually, string messageTypeName = "")
        {
            if (logFile == null)
                return;

            var str = String.IsNullOrEmpty(secondIndentification) ? "" : secondIndentification + "\r\n";
            lock (this)
                File.AppendAllText(logFile.FullName, isError.ToString() + "\t" + getDateTime() + $"; pid = {PID}\r\n" + str + Message + "\r\n----------------------------------------------------------------\r\n\r\n", utf8);
        }
    }
}
