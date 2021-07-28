# socks5-proxy
simple socks5-proxy by C#/Mono (currently not finished; пока не закончено)

Русский ниже

To translate to English
https://translate.yandex.ru/?lang=ru-en

Socks5 proxy for host in home usage.

Need Mono in Linux ( https://www.mono-project.com/ ) and .NET Framework 4.8 in Windows ( https://dotnet.microsoft.com/download )

Completed functionality:
* Can listen on multiple ports
* Login and password authentication (need restart server for user add)
* Admin can denied IP addresses (only domain names allowed; carefully, perhaps the function can be bypassed)

Disadvantages:
* no have reload operation for config (available only for the trusts file)

Example of configuration
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/vinny-socks5-proxy/vinny-socks5-proxy.conf.txt
* (not working yet) https://github.com/fdsc/vinny-socks5-proxy/blob/main/trusts/example.trusts

---------------
Русский

Socks5 прокси для домашнего использования на своей машине.

Требует Mono на Linux ( https://www.mono-project.com/ ) и .NET Framework 4.8 на Windows ( https://dotnet.microsoft.com/download )

Законченная функциональность
* Может прослушивать несколько портов
* Аутентификация по логину и паролю (для добавления пользователя требуется перезапуск сервера)
* Администратор может запретить пересылку данных по IP-адресам (оставив только доменные имена; осторожно, возможно, данное ограничение можно обойти)

Недостатки:
* не возможности перезагрузить конфигурацию во время выполнения (доступно только для trusts-файла)

Пример конфигурации
* https://github.com/fdsc/vinny-socks5-proxy/blob/main/vinny-socks5-proxy/vinny-socks5-proxy.conf.txt
* (пока не работает) https://github.com/fdsc/vinny-socks5-proxy/blob/main/trusts/example.trusts
