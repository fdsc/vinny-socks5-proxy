# socks5-proxy
simple socks5-proxy by C#/Mono (currently not finished; пока не закончено)

Русский ниже

To translate to English
https://translate.yandex.ru/?lang=ru-en

Socks5 proxy for host in home usage.

Need Mono in Linux ( https://www.mono-project.com/ ) and .NET Framework in Windows ( https://dotnet.microsoft.com/download )

Completed functionality:
* Can listen on multiple ports
* Login and password authentication (need restart server for user add)
* IPv4 addresses restriction (if needed)

Disadvantages:
* Creates two system threads for an each client connection (load on the system; cannot handle many connections)

Example of configuration
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/vinny-socks5-proxy/vinny-socks5-proxy.conf.txt
* (not working yet) https://github.com/fdsc/vinny-socks5-proxy/blob/main/trusts/example.trusts

---------------
Русский

Socks5 прокси для домашнего использования на своей машине.

Требует Mono на Linux ( https://www.mono-project.com/ ) и .NET Framework на Windows ( https://dotnet.microsoft.com/download )

Законченная функциональность
* Может прослушивать несколько портов
* Аутентификация по логину и паролю (для добавления пользователя требуется перезапуск сервера)
* Запрет на пересылку IP-адресов, если необходимо

Недостатки:
* На каждое клиентское соединение создаёт два системных потока (нагрузка на систему; не может обрабатывать много соединений)

Пример конфигурации
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/vinny-socks5-proxy/vinny-socks5-proxy.conf.txt
* (пока не работает) https://github.com/fdsc/vinny-socks5-proxy/blob/main/trusts/example.trusts
