# DomainConsole 0.20

В версии 0.20 исправлена запись статуса при блокировке файла, диагностика отделена по заданиям, вывод CBS ограничен контекстом. Добавлены подсветка и переходы поиска, очистка окна журнала, расшифровка частых ошибок и пять повторов поиска для 0x80244010. Несогласованный объём WUA помечается как уточняемый.

# DomainConsole

Windows C# / WPF domain administration console, version 0.3.

Editable source is in `DomainConsole-CSharp/`. The ZIP at the repository root is the original 0.2 source snapshot; builds use the editable source tree.

GitHub Actions builds the Windows x64 portable app and verifies that its window starts and closes. Download `DomainConsole-Windows-x64` from a successful workflow run; extract all files and launch `DomainConsole.exe` with the adjacent `Agent` folder.

Version 0.3 adds direct editor command execution, automatic result display, connection checks, readable PowerShell errors, Russian connection statuses, and copy/export controls. Domain connectivity and remote execution require testing in your environment.

