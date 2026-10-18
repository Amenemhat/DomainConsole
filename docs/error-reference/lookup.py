"""Offline reference lookup. No network calls, repairs, or DomainConsole state changes."""
import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent

def unsigned32(value):
    if isinstance(value, bool): raise ValueError('Boolean is not a code')
    if isinstance(value, int): number=value
    elif isinstance(value,str):
        value=value.strip()
        number=int(value,16) if value.lower().startswith('0x') else int(value,10)
    else: raise ValueError('Expected an integer or numeric string')
    if number < -(2**31) or number > 2**32-1: raise ValueError('Outside 32-bit range')
    return number & 0xFFFFFFFF

def lookup(value, domain, program=None):
    if domain not in ('hresult','win32','exit','wua-result','event'):
        raise ValueError('Explicit supported code domain required')
    n=unsigned32(value)
    if domain=='exit':
        return dict(domain=domain,value=n,program=program,status='program_contract_required',
            message_ru='Код завершения программы. Нужен контракт конкретной команды; не расшифровывать его как HRESULT или Win32 автоматически.')
    if domain=='event':
        return dict(domain=domain,value=n,status='provider_required',
            message_ru='Номер события не является HRESULT. Нужны provider/channel и данные события.')
    if domain=='wua-result':
        states={0:'Не начато',1:'Выполняется',2:'Успешно',3:'Успешно с ошибками',4:'Неудача',5:'Отменено'}
        return dict(domain=domain,value=n,status='known' if n in states else 'unknown',
            message_ru=states.get(n,'Неизвестное состояние ResultCode'),
            requires_item_hresult=n in (3,4),automatic_repair_allowed=False)
    if domain=='win32':
        raw=next((x for x in json.loads((ROOT/'win32-symbols.json').read_text()) if x['value']==n),None)
        if raw is None:
            return dict(domain=domain,value=n,status='unknown',message_ru='Код Win32 не найден в этом снимке справочника.')
        code=raw['hresult']
    else: code=f'0x{n:08X}'
    entry=next((x for x in json.loads((ROOT/'catalog.json').read_text())['entries'] if x['code']==code),None)
    if entry is None:
        return dict(domain=domain,code=code,status='unknown',automatic_repair_allowed=False,
            message_ru='Код не найден в справочнике. Сохраните системное сообщение и контекст; доступен поиск по точному коду.')
    return dict(domain=domain,input_value=n,status=entry['coverage'],entry=entry,
        message_ru=entry.get('card',{}).get('meaning_ru',
            'Техническое имя известно; подробная русская диагностическая карточка пока не подготовлена.'))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('code')
    parser.add_argument('--domain',required=True,choices=['hresult','win32','exit','wua-result','event'])
    parser.add_argument('--program')
    args=parser.parse_args()
    print(json.dumps(lookup(args.code,args.domain,args.program),ensure_ascii=False,indent=2))
