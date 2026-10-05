using System;
namespace DomainConsole.Shared {
 public static class ExecutionDiagnosis {
  public static string Code(int code)=>code+" (0x"+unchecked((uint)code).ToString("X8")+")";
  public static string Explain(int code){switch(unchecked((uint)code)){case 0x80072EE2:return "Истекло время сетевого запроса. Проверьте источник восстановления/обновлений, доступ из SYSTEM, прокси и политики. Конкретный сервер этим кодом не определяется.";case 0x800F081F:return "Не найдены исходные файлы восстановления. Проверьте подходящий источник и индекс образа.";case 5:case 0x80070005:return "Отказано в доступе. Проверьте права и разрешения.";default:return "Команда завершилась с ненулевым кодом. Подробности смотрите в выводе и журналах; код зависит от запущенной программы.";}}
 }
}
