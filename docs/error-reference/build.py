"""Build a factual code index and separately reviewed Russian diagnostic cards.

No source descriptions are copied into the generated catalog. Numeric/symbol
pairs are facts; Russian text is authored in cards.tsv. This is reference data,
not a deployment or repair tool.
"""
import collections
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
DATE = '2026-10-18'
SDK = 'https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/'
SOURCES = {
    'wuerror.h': SDK + 'um/wuerror.h',
    'BitsMsg.h': SDK + 'um/BitsMsg.h',
    'winhttp.h': SDK + 'um/winhttp.h',
    'cbs.md': 'https://learn.microsoft.com/en-us/troubleshoot/azure/virtual-machines/windows/windows-update-errors-requiring-in-place-upgrade',
    'repair': 'https://learn.microsoft.com/en-us/troubleshoot/windows-server/installing-updates-features-roles/fix-windows-update-errors',
    'delta': 'https://learn.microsoft.com/en-us/windows/deployment/update/psfxwhitepaper',
    'defender': 'https://learn.microsoft.com/en-us/defender-endpoint/command-line-arguments-microsoft-defender-antivirus',
    'defender-codes': 'https://learn.microsoft.com/en-us/defender-endpoint/troubleshoot-microsoft-defender-antivirus',
    'com': 'https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-3',
    'com-general': 'https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1',
    'com-init': 'https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-coinitializeex',
    'crypto': 'https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-4',
    'lookup': 'https://learn.microsoft.com/en-us/windows/win32/debug/system-error-code-lookup-tool',
    'com1': 'https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-1',
    'com3': 'https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-3',
    'com4': 'https://learn.microsoft.com/en-us/windows/win32/com/com-error-codes-4',
}
# These are diagnostic plans, not code definitions or executable repairs.
PROFILES = {
 'result': ('Проверить итог операции', ['Источник кода, ResultCode, результат каждого элемента', 'Актуальные флаги и время завершения'], ['Не трактовать exit code процесса как HRESULT без указания домена']),
 'reboot': ('Уточнить необходимость перезагрузки', ['WUA SystemInfo и ключи Windows Update/CBS', 'BootUtc и время предыдущего положительного флага'], ['Перезагрузка только с отдельным разрешением', 'После возврата заново читать флаги и продолжать разрешённую цепочку']),
 'busy': ('Проверить активную операцию', ['WUA Installer.IsBusy, активное задание и время его начала', 'Динамика CPU/диска/событий; два снимка вместо одного процента'], ['Не перезапускать службы во время подтверждённой установки автоматически', 'Неподвижный процент не доказывает зависание']),
 'service': ('Проверить службы', ['Состояние, тип запуска, PID и зависимые службы', 'События Service Control Manager за интервал операции'], ['Принудительная остановка требует проверки общего процесса и согласия', 'Не завершать общий svchost по имени службы без проверки']),
 'metadata': ('Проверить метаданные обновления', ['UpdateID, ревизия, критерий поиска, фактический источник', 'WindowsUpdate.log и состояние синхронизации WSUS'], ['Сброс DataStore теряет историю/состояние и не является универсальным первым действием']),
 'policy': ('Проверить применённые политики', ['Результирующие GPO, WUServer/WUStatusServer/UseWUServer', 'Контекст SYSTEM и действующие ограничения'], ['Не менять корпоративные политики автоматически', 'Сброс кэша не отменяет ограничения политик']),
 'access': ('Проверить контекст доступа', ['Учётная запись, повышение прав, способ аутентификации', 'Объект отказа: файл, служба, COM или удалённый endpoint'], ['Не хранить пароли в журнале', 'Не отключать UAC/ACL/защиту ради обхода ошибки']),
 'applicability': ('Проверить применимость', ['Версия/сборка ОС, архитектура, язык, редакция', 'Предпосылки и замещение обновления'], ['Неприменимость не доказывает повреждение Windows']),
 'network': ('Проверить соединение с источником', ['DNS, маршрут, TCP, затем реальный HTTP-ответ', 'Прокси, VPN, firewall и адрес запроса'], ['TCP доступен не означает HTTP/TLS/авторизация исправны', 'Потеря связи не означает отмену удалённой операции']),
 'dns': ('Проверить разрешение имени', ['Точное имя источника/прокси и DNS-ответ', 'VPN-маршрут, DNS-суффикс, контекст службы'], ['Не заменять источник произвольным IP без проверки TLS и идентичности']),
 'timeout': ('Определить где истекло ожидание', ['URL, этап, лимит ожидания, длительность и движение байтов', 'Время ответа IIS/прокси и нагрузка клиента'], ['Использовать ограниченные повторы с паузой', 'На медленном канале оценивать реальный прогресс и размер']),
 'wrapper': ('Извлечь исходную причину', ['Внутренний HRESULT, GetUpdateResult, UnmappedResultCode', 'Первая относящаяся к пакету ошибка CBS/MSI/BITS'], ['Общий код не позволяет выбрать конкретное лечение']),
 'application': ('Проверить вызывающую команду/API', ['CMD или PowerShell, аргументы, разрядность, STA/MTA', 'Метод API и внутреннее исключение'], ['Синтаксическая ошибка команды не требует сброса Windows Update']),
 'content': ('Проверить конкретное содержимое', ['Имя файла, пакет, хэш/подпись, фактический Content URL', 'Состояние передачи и существование файла на WSUS'], ['Очищать только установленный проблемный кэш после завершения операций', 'Не удалять WinSxS или системные пакеты вручную']),
 'component': ('Проверить обслуживание компонентов', ['Первичная ошибка CBS с пакетом/файлом и временем', 'DISM CheckHealth/ScanHealth; история обслуживания'], ['RestoreHealth выбирается после диагностики с подходящим источником', 'Не удалять пакеты SSU/LCU по одному совпавшему коду']),
 'repairsource': ('Проверить источник восстановления', ['Точный компонент и требуемая версия из CBS/DISM', 'Доступность источника и соответствие ОС, редакции, языка, архитектуры и уровню обслуживания'], ['Обычный WSUS не гарантирует наличие содержимого для ремонта', 'Не отключать UseWUServer автоматически']),
 'delta': ('Проверить базовые файлы и дельта-пакет', ['Первая ошибка pre-stage/hydration и имя файла из CBS', 'Версии компонентов, целостность пакета и хранилища'], ['Код не различает автоматически повреждение загрузки и базового компонента', 'Не повторять полный сброс вслепую после каждой попытки']),
 'file': ('Проверить объект и путь', ['Полный путь/имя объекта, существование, доступ и время ошибки', 'Связь с текущим пакетом и операцией'], ['Файл агента и файл Windows Update требуют разных действий']),
 'locked': ('Найти владельца блокировки', ['Путь и процесс, который держит файл', 'Другие агенты, установщики, антивирус/backup; режим открытия файла'], ['status.json: исправлять конкурентную запись/чтение агента', 'Не рекомендовать очистку Download для блокировки файла агента']),
 'memory': ('Проверить ресурсы памяти', ['Физическая память, commit/limit, pagefile и свободное место его томов', 'Потребление процессов и события Resource-Exhaustion-Detector'], ['Нехватка памяти не тождественна нехватке дискового места', 'Изменение pagefile может требовать перезагрузки']),
 'disk': ('Проверить том и файловую систему', ['Путь, свободное место и события Disk/NTFS/storage', 'Результат CHKDSK и доступность чтения конкретного каталога'], ['Не считать успешное завершение предыдущего CHKDSK доказательством исправности любого пути', 'Офлайн-проверка/перезагрузка согласуются отдельно']),
 'bitsqueue': ('Проверить очередь BITS', ['JobId, владелец, состояние, Creation/ModificationTime', 'Связь задания с WSUS по URL и локальному пути'], ['Не удалять все BITS-задания: среди них могут быть чужие приложения']),
 'cost': ('Проверить ограничения стоимости сети', ['TransferPolicy конкретного задания, стоимость соединения и GPO', 'Владелец задания и доступный ему сетевой интерфейс'], ['Ускорение и снятие лимитов только как явное административное действие', 'Очистка Download не исправит запрет политики']),
 'tls': ('Проверить защищённое соединение', ['Системные часы, цепочка сертификатов, имя узла', 'Поддержка TLS, корневые сертификаты и TLS-инспекция'], ['Не отключать проверку сертификата и не включать устаревшие протоколы автоматически']),
 'signature': ('Проверить доверие и целостность', ['Подпись и хэш конкретного файла', 'Время проверки, временная метка и цепочка доверия'], ['Не устанавливать неподписанное содержимое и не отключать проверку подписи']),
 'defender': ('Проверить работоспособность Defender', ['WinDefend, Get-MpComputerStatus, версии платформы/движка/сигнатур', 'Defender Operational и MpCmdRun.log; применимость команды к ОС'], ['MRT и Defender — разные компоненты', 'Сброс платформы не назначать автоматически по таймауту MAPS']),
 'server': ('Проверить обработку на WSUS', ['URL, HTTP status/substatus, IIS/HTTPERR и WsusPool', 'WSUS events/SoftwareDistribution.log и доступность SUSDB'], ['Клиентский поиск и синхронизация самого WSUS с Microsoft — разные операции']),
 'roundtrips': ('Продолжить получение метаданных', ['Фактический SyncUpdates round trips из журнала', 'История повторов и изменения результата между попытками'], ['Число обменов не равно числу обновлений или метаданных из общего количества', 'Универсальный обязательный интервал Microsoft не задаёт: пауза и предел повторов — настройка приложения', 'Не очищать накопленный кэш перед каждой повторной попыткой']),
 'http401': ('Проверить авторизацию HTTP 401', ['Точный endpoint и настройки аутентификации IIS', 'Владелец запроса, заголовок вызова авторизации без секретов'], ['Не передавать учётные данные на неожиданный/непроверенный endpoint']),
 'http403': ('Проверить запрет HTTP 403', ['IIS substatus и правило ограничения', 'Прокси/firewall, клиентский адрес и разрешение URL'], ['Открытый TCP-порт не отменяет запрет HTTP']),
 'http404': ('Проверить ресурс HTTP 404', ['Полный относительный путь запроса', 'Веб-сервис WSUS или конкретный Content-файл'], ['Сначала исправить источник, а не бесконечно очищать клиентскую загрузку']),
 'proxy407': ('Проверить авторизацию прокси', ['WinHTTP proxy и служебный контекст', 'Способ авторизации, firewall и HTTP-код ответа'], ['Авторизация пользователя в браузере не гарантирует доступ SYSTEM']),
 'http503': ('Проверить доступность веб-службы', ['WsusPool, очередь HTTP и причины остановки/переработки пула', 'Нагрузка IIS/SUSDB и Retry-After если получен'], ['Повторять с ограничением, не запускать одновременный сброс всех клиентов']),
}

def build():
    entries = {}
    def add(code, symbol, source, **extra):
        code = f'0x{int(code, 16):08X}'
        e = entries.setdefault(code, dict(code=code, domain='hresult', symbols=[], sources=[], **extra))
        if symbol and symbol not in e['symbols']: e['symbols'].append(symbol)
        if source and source not in e['sources']: e['sources'].append(source)
        return e
    for x in json.loads((ROOT/'sdk-symbols.json').read_text()):
        add(x['code'], x['symbol'], SOURCES[x['source']])
    win32 = json.loads((ROOT/'win32-symbols.json').read_text())
    for x in win32:
        # HRESULT_FROM_WIN32: do not mistake this for raw command exit code.
        add(x['hresult'], x['symbol'], x['source'], win32_value=x['value'])
    cards = []
    for line in (ROOT/'cards.tsv').read_text().splitlines():
        if not line or line.startswith('#'): continue
        fields = line.split('|')
        assert len(fields)==7, line
        code, severity, profile, symbol, meaning, causes, action = fields
        assert profile in PROFILES, profile
        extra = []
        if profile in ('component','delta','repairsource'): extra += [SOURCES['repair']]
        if profile == 'delta': extra += [SOURCES['delta']]
        if code == '0x800106BA': extra += [SOURCES['defender']]
        if code.startswith('0x8050'): extra += [SOURCES['defender-codes']]
        if code == '0x80010106': extra += [SOURCES['com'],SOURCES['com-init']]
        if code in ('0x80040154','0x80004005','0x00000001'): extra += [SOURCES['com-general']]
        if profile == 'signature' or code == '0x80092003': extra += [SOURCES['crypto']]
        e = add(code, None if symbol in ('-','?') else symbol, extra[0] if extra else None)
        for source in extra:
            if source not in e['sources']: e['sources'].append(source)
        assert e['sources'], code+' has no source'
        assert symbol != '-' or e['symbols'], code+' missing symbol'
        assert 'card' not in e, code+' duplicated card'
        title, diagnostics, cautions = PROFILES[profile]
        e['card'] = dict(editorial_status='prepared_for_review', severity=severity,
            meaning_ru=meaning, possible_causes_ru=[causes], next_action_ru=action,
            diagnostic_profile=profile, diagnostic_title_ru=title,
            checks_ru=diagnostics, constraints_ru=cautions,
            automatic_repair_allowed=False, explanation_is_not_diagnosis=True)
        cards.append(e)
    for e in entries.values():
        value = int(e['code'],16)
        e['signed32'] = value if value<2**31 else value-2**32
        e['failure_bit'] = bool(value & 0x80000000)
        e['coverage'] = 'russian_diagnostic_card' if 'card' in e else 'technical_symbol_only'
        e['reference_marker'] = any('_ERROR_CONTEXT_' in s or s.endswith('_BASE') for s in e['symbols'])
        # Markers and failure bit do not decide outcome/severity without API context.
        e['verified_at'] = DATE
    catalog = dict(schema_version=1, reference_date=DATE,
        scope='Reference data only; not yet integrated into DomainConsole runtime',
        interpretation_policy='See README.md; match namespace and operation before rendering a card',
        counts=dict(hresult_values=len(entries),win32_values=len(win32),russian_cards=len(cards),
            technical_only=sum('card' not in e for e in entries.values())),
        entries=sorted(entries.values(),key=lambda e:e['code']))
    (ROOT/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2)+'\n')
    text = ['# Справочник ошибок обслуживания Windows', '',
        f'Снимок источников: {DATE}. Значений HRESULT: **{len(entries)}**, исходных Win32: **{len(win32)}**, подробных русских карточек: **{len(cards)}**.', '',
        'Это подготовленные справочные данные, ещё не включённые в приложение. Возможные причины — гипотезы, пока они не подтверждены журналом конкретной попытки. В каталоге также есть успешные, ожидающие и служебные коды.', '',
        '## Подробные карточки', '']
    for e in sorted(cards,key=lambda e:e['code']):
        c=e['card']; symbol=' / '.join(e['symbols']) or 'официальное символическое имя в выбранном источнике не указано'
        text += [f"### {e['code']} — {symbol}", '', c['meaning_ru'], '',
            '**Возможные причины:** '+ ' '.join(c['possible_causes_ru']), '',
            '**Первое действие:** '+c['next_action_ru'], '',
            '**Проверить:** '+'; '.join(c['checks_ru'])+'.', '',
            '**Условия:** '+'; '.join(c['constraints_ru'])+'.', '',
            '**Источники определения:** '+ ' · '.join(f'[Microsoft {i+1}]({s})' for i,s in enumerate(e['sources'])), '']
    text += ['## Технический индекс остальных кодов', '',
        'Подробное русское объяснение этих значений пока не подготовлено. Не подставлять общую рекомендацию вместо отсутствующей карточки. Источники каждой записи — в catalog.json.', '',
        '| HRESULT | Техническое имя |', '|---|---|']
    text += [f"| `{e['code']}` | `{' / '.join(e['symbols'])}` |" for e in catalog['entries'] if 'card' not in e]
    (ROOT/'REFERENCE.ru.md').write_text('\n'.join(text)+'\n')
    checksums = {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in [ROOT/'sdk-symbols.json',ROOT/'win32-symbols.json',ROOT/'cards.tsv',ROOT/'catalog.json',ROOT/'REFERENCE.ru.md']}
    (ROOT/'manifest.json').write_text(json.dumps(dict(date=DATE,counts=catalog['counts'],sha256=checksums),indent=2)+'\n')
    print(json.dumps(catalog['counts']))

if __name__=='__main__': build()
