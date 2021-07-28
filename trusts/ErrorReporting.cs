using System;
using System.Diagnostics;

namespace trusts
{
    /// <summary>Базовый класс, определяющий способ логирования ошибок</summary>
    public class ErrorReporting
    {
        /// <summary>Строка, идентифицирующая процесс</summary>
        public readonly String   PID            = Process.GetCurrentProcess().Id.ToString();

        /// <summary>Строка идентификации лога (идентифицирует, кто записиывает в лог)</summary>
        public volatile String   Identification = "";

        /// <summary>Базовый конструктор</summary>
        public ErrorReporting()
        {
        }

        /// <summary>Определяет типа сообщения об ошибке: обычное сообщение, предупреждение или ошибки</summary>
        public enum LogTypeCode
        {                                       /// <summary>Обычное сообщение</summary>
            Usually    = 0,                     /// <summary>Изменение режима работы программы</summary>
            Changed    = 1,                     /// <summary>Предупреждение: работа программы не нарушена, но событие говорит о возможной проблеме или опасности</summary>
            Wargning   = 2,                     /// <summary>Небольшая ошибка. Программа в целом работает</summary>
            SmallError = 3,                     /// <summary>Серьёзная ошибка, затрудняющая работу программы. Возможно, может быть исправлена пользователем (неверная конфигурация)</summary>
            Error      = 4,                     /// <summary>Фатальная ошибка, программа не будет работать или будет работать неверно</summary>
            FatalError = 5
        }

        /// <summary>Логирует сообщение (в базовом классе ничего не делает)</summary>
        /// <param name="Message">Сообщение для логирования</param>
        /// <param name="secondIndentification">Дополнительное идентификационное сообщение</param>
        /// <param name="isError">Общий тип сообщения об ошибке</param>
        /// <param name="messageTypeName">Тип сообщения об ошибке</param>
        public virtual void Log(string Message, string secondIndentification, LogTypeCode isError = LogTypeCode.Usually, string messageTypeName = "")
        {
        }
    }
}
