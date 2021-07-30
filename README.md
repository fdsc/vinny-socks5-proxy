# socks5-proxy
simple socks5-proxy by C#/Mono

Русский ниже

To translate to English
https://translate.yandex.ru/?lang=ru-en

Socks5 и http proxy for host in home usage.

Need Mono in Linux ( https://www.mono-project.com/ ) and .NET Framework 4.8 in Windows ( https://dotnet.microsoft.com/download )

Completed functionality:
* Can listen on multiple ports
* Login and password authentication (need restart server for user add)
* It is possible to differentiate access (for port, not for a user) by a trusts configuration file with a change in the file without stopping the server
* Both the socks5 and the http proxy working on the same port

Disadvantages:
* no have reload operation for config (available only for the trusts file)
* complex language for configuring the trust file configuration
* only the TCP CONNECT command is supported. This is enough for most home applications (browsers, disks, etc.), but this is not all the socks5 possibilities


Example of configuration
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/vinny-socks5-proxy/vinny-socks5-proxy.conf.txt
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/trusts/example3.trusts (and files near)


---------------
Русский

Socks5 и http прокси для домашнего использования на своей машине.

Требует Mono на Linux ( https://www.mono-project.com/ ) и .NET Framework 4.8 на Windows ( https://dotnet.microsoft.com/download )

Законченная функциональность
* Может прослушивать несколько портов
* Аутентификация по логину и паролю (для добавления пользователя требуется перезапуск сервера)
* Возможно разграничение доступа (для порта, не для пользователя) по файлу конфигурации доверия с изменением списка без прекращения работы сервера 
* На одном порту поднимается как socks5, так и http-прокси

Недостатки:
* не возможности перезагрузить конфигурацию во время выполнения (доступно только для trusts-файла)
* сложный язык настройки конфигурации файла доверия
* поддерживается только команда TCP CONNECT. Этого достаточно для большинства домашних приложений (браузеров, дисков и т.п.), но это не все возможности socks5


Пример конфигурации
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/vinny-socks5-proxy/vinny-socks5-proxy.conf.txt
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/trusts/example3.trusts (и файлы рядом)
