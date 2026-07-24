using System.Globalization;

namespace InfoWatchUninstaller
{
    internal static class Loc
    {
        public static bool IsRu => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru";
        private static bool IsRussian => IsRu;

        // Заголовки и метки
        public static string ProductLabel => IsRussian ? "Имя продукта (для поиска):" : "Product name (search):";
        public static string ServiceLabel => IsRussian ? "Имя службы-посредника:" : "Mediator service name:";
        public static string CommandLabel => IsRussian ? "Команда для выполнения (опцион.):" : "Command to execute (optional):";
        public static string MaxWaitLabel => IsRussian ? "Макс. время ожидания (сек):" : "Max wait time (sec):";
        public static string CheckIntervalLabel => IsRussian ? "Интервал проверки (сек):" : "Check interval (sec):";
        public static string UninstallButton => IsRussian ? "Запустить деинсталляцию" : "Start uninstall";
        public static string StopButton => IsRussian ? "⏹ Стоп" : "⏹ Stop";
        public static string NetworkCheckbox => IsRussian ? "Отключить сеть на время удаления" : "Disable network during uninstall";
        public static string MethodGroup => IsRussian ? "Метод удаления" : "Uninstall method";
        public static string ServiceRadio => IsRussian ? "Через службу" : "Via service";
        public static string DirectRadio => IsRussian ? "Прямой запуск" : "Direct launch";
        public static string SystemRadio => IsRussian ? "Через SYSTEM" : "Via SYSTEM";
        public static string StatusReady => IsRussian ? "Готов к работе" : "Ready";
        public static string InstalledAppsLabel => IsRussian ? "Установленные приложения:" : "Installed applications:";
        public static string RefreshButton => IsRussian ? "🔄 Обновить список" : "🔄 Refresh list";
        public static string BrowseButton => IsRussian ? "Обзор…" : "Browse…";
        public static string HelpButton => IsRussian ? "?" : "?";
        public static string Title => IsRussian ? "Деинсталлятор (служба / прямой / SYSTEM)" : "Uninstaller (service / direct / SYSTEM)";

        // Диалоги — заголовки
        public static string TitleBusy => IsRussian ? "Занято" : "Busy";
        public static string TitleError => IsRussian ? "Ошибка" : "Error";
        public static string TitleConfirmUninstall => IsRussian ? "Подтверждение удаления" : "Confirm uninstall";
        public static string TitleInsufficientRights => IsRussian ? "Недостаточно прав" : "Insufficient rights";
        public static string TitleFileNotFound => IsRussian ? "Файл не найден" : "File not found";
        public static string TitleAbout => IsRussian ? "О программе" : "About";
        public static string TitleConfirmRegistryDelete => IsRussian ? "Подтверждение удаления записи" : "Confirm registry deletion";
        public static string TitleDone => IsRussian ? "Готово" : "Done";
        public static string TitleAdminRequired => IsRussian ? "Требуются права администратора" : "Administrator rights required";
        public static string TitleUpdateAvailable => IsRussian ? "Обновление доступно" : "Update available";
        public static string TitleUpdateError => IsRussian ? "Ошибка обновления" : "Update error";

        // Диалоги — сообщения
        public static string OperationAlreadyRunning => IsRussian ? "Операция уже выполняется." : "Operation is already running.";
        public static string EnterProductName => IsRussian ? "Введите имя продукта." : "Enter the product name.";
        public static string ServiceNameRequired => IsRussian ? "При использовании метода через службу укажите имя службы." : "When using the service method, specify a service name.";
        public static string MethodViaService => IsRussian ? "через службу" : "via service";
        public static string MethodDirect => IsRussian ? "прямым запуском" : "direct launch";
        public static string MethodSystem => IsRussian ? "от имени SYSTEM" : "as SYSTEM";
        public static string OpenFileDialogTitle => IsRussian ? "Выберите файл для выполнения" : "Choose a file to execute";
        public static string OpenFileDialogFilter => IsRussian
            ? "Исполняемые файлы (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|Все файлы (*.*)|*.*"
            : "Executable files (*.exe;*.bat;*.cmd)|*.exe;*.bat;*.cmd|All files (*.*)|*.*";
        public static string RestartAsAdminFailed(string message) => IsRussian
            ? $"Не удалось запустить с правами администратора: {message}"
            : $"Could not restart with administrator rights: {message}";
        public static string ProductRemovedSuccess => IsRussian ? "Продукт успешно удалён." : "Product successfully removed.";
        public static string ProcessFailedToStart => IsRussian ? "Не удалось запустить процесс." : "Process failed to start.";
        public static string RegistryRemoveFailed => IsRussian ? "Не удалось удалить запись реестра." : "Could not remove registry entry.";

        public static string AdminRequired => IsRussian ? "Требуются права администратора. Перезапустить приложение?" : "Administrator rights required. Restart the application?";
        public static string AdminStartupMessage => IsRussian
            ? "Программа требует права администратора для:\n• управления службами Windows\n• доступа к реестру (удаление/изменение)\n• отключения сетевых адаптеров\n\nНажмите «Да», чтобы перезапустить программу с повышенными правами.\nЕсли у вас нет прав администратора, обратитесь к системному администратору."
            : "This program requires administrator rights for:\n• managing Windows services\n• registry access (delete/modify)\n• disabling network adapters\n\nClick Yes to restart with elevated rights.\nIf you do not have administrator rights, contact your system administrator.";
        public static string AdminStartupFailed => IsRussian
            ? "Не удалось получить права администратора.\nУбедитесь, что ваша учётная запись имеет права администратора, или запустите программу вручную от имени администратора."
            : "Could not obtain administrator rights.\nMake sure your account has administrator privileges, or run the program manually as administrator.";

        public static string ConfirmUninstall(string product, string method, string netStatus) =>
            IsRussian ?
            $"Вы действительно хотите удалить продукт, содержащий \"{product}\"?\n\n• Метод: {method}.\n• Будет выполнена попытка принудительного удаления.\n• {netStatus}\n• Может потребоваться перезагрузка компьютера.\n\nПродолжить?" :
            $"Are you sure you want to remove the product containing \"{product}\"?\n\n• Method: {method}.\n• An attempt to force uninstall will be made.\n• {netStatus}\n• A reboot may be required.\n\nContinue?";
        public static string NetworkOff => IsRussian ? "Сетевые адаптеры будут временно отключены для блокировки связи." : "Network adapters will be temporarily disabled to block communication.";
        public static string NetworkOn => IsRussian ? "⚠️ ВНИМАНИЕ: Сеть останется включенной! Агент может отправить алерт о попытке удаления." : "⚠️ WARNING: Network will stay enabled! The agent may send an alert about the removal attempt.";

        // Тултипы
        public static string TooltipProductLabel => IsRussian
            ? "Введите часть названия программы, которую хотите удалить.\nМожно также выбрать программу из списка справа — имя подставится автоматически."
            : "Enter part of the program name you want to remove.\nYou can also select a program from the list on the right — the name will be filled in automatically.";
        public static string TooltipServiceLabel => IsRussian
            ? "Служба Windows, ImagePath которой будет временно заменён на команду удаления.\nАктуально только для метода «Через службу»."
            : "Windows service whose ImagePath will be temporarily replaced with the uninstall command.\nApplies only to the Via service method.";
        public static string TooltipCommandLabel => IsRussian
            ? "Пользовательская команда, которая будет выполнена вместо автоматически сгенерированной.\nОставьте поле пустым, чтобы программа сама определила команду удаления."
            : "Custom command to run instead of the automatically generated one.\nLeave empty to let the program determine the uninstall command.";
        public static string TooltipProductName => IsRussian
            ? "Поиск продукта в реестре Uninstall по подстроке.\nНапример, «InfoWatch» найдёт «InfoWatch Device Monitor»."
            : "Search for a product in the Uninstall registry by substring.\nFor example, \"InfoWatch\" will find \"InfoWatch Device Monitor\".";
        public static string TooltipServiceName => IsRussian
            ? "Рекомендуется использовать некритичную службу (Spooler, wuauserv, upnphost).\nНе используйте системные службы, от которых зависит стабильность ОС."
            : "Use a non-critical service (Spooler, wuauserv, upnphost).\nDo not use system services that OS stability depends on.";
        public static string TooltipCustomCommand => IsRussian
            ? "Можно указать путь к EXE, BAT, CMD или любую консольную команду.\nИспользуйте кнопку «Обзор…» для выбора файла."
            : "You can specify a path to EXE, BAT, CMD, or any console command.\nUse the Browse button to select a file.";
        public static string TooltipServiceMethod => IsRussian
            ? "Выполняет удаление путём подмены исполняемого файла выбранной службы.\nКоманда запускается от имени SYSTEM в изолированной сессии 0.\nМожет потребоваться для обхода мониторинга агентов, но есть риск несовместимости\nс некоторыми установщиками (особенно MSI)."
            : "Uninstalls by replacing the selected service executable.\nThe command runs as SYSTEM in isolated session 0.\nMay help bypass agent monitoring, but may be incompatible\nwith some installers (especially MSI).";
        public static string TooltipDirectMethod => IsRussian
            ? "Запускает команду удаления напрямую в фоновом процессе из текущей сессии\nс правами администратора. Более надёжный способ для большинства программ.\nРекомендуется для MSI-пакетов и установщиков, требующих доступа к профилю пользователя."
            : "Runs the uninstall command directly in a background process from the current session\nwith administrator rights. More reliable for most programs.\nRecommended for MSI packages and installers that need user profile access.";
        public static string TooltipSystemMethod => IsRussian
            ? "Копирует токен процесса winlogon.exe и запускает команду от имени SYSTEM\nвне службы (без подмены ImagePath). Позволяет получить права SYSTEM\nбез ошибок, связанных со службой, но сохраняет ограничения изолированной сессии.\nПолезен, если нужно выполнить команду именно с максимальными привилегиями."
            : "Copies the winlogon.exe process token and runs the command as SYSTEM\noutside a service (without ImagePath replacement). Gets SYSTEM rights\nwithout service-related errors, but keeps isolated session limitations.\nUseful when you need maximum privileges.";
        public static string TooltipInstalledAppsList => IsRussian
            ? "Выберите приложение из списка — имя будет автоматически подставлено в поле поиска.\nДвойной клик открывает подробную информацию о программе."
            : "Select an application from the list — the name will be filled in the search field.\nDouble-click opens detailed program information.";
        public static string TooltipInstalledAppsLabel => IsRussian
            ? "Список всех программ, зарегистрированных в системе.\nДвойной клик по элементу списка показывает детали и позволяет удалить запись реестра."
            : "List of all programs registered in the system.\nDouble-click an item to view details and delete the registry entry.";

        public static string HelpText => IsRussian
            ? "SysUninstaller – утилита для принудительного удаления программ\n\n📋 Требования:\n• Права администратора (запрашиваются при запуске)\n• .NET Framework 4.5.2 или выше\n• Windows 7 / 8 / 10 / 11\n\n🛠 Основные возможности:\n• Удаление программ через службу / прямой запуск / от SYSTEM\n• Очистка «висящих» записей реестра\n• Временное отключение сети для предотвращения обратной связи\n\n📖 Подробная инструкция: https://github.com/kizrrum/SysUninstaller#readme"
            : "SysUninstaller – utility for forced program removal\n\n📋 Requirements:\n• Administrator rights (requested at startup)\n• .NET Framework 4.5.2 or higher\n• Windows 7 / 8 / 10 / 11\n\n🛠 Features:\n• Uninstall via service / direct launch / as SYSTEM\n• Clean up orphaned registry entries\n• Temporarily disable network to prevent callbacks\n\n📖 Full guide: https://github.com/kizrrum/SysUninstaller#readme";

        // Форма деталей продукта
        public static string ProductDetailsTitle => IsRussian ? "Информация о продукте" : "Product information";
        public static string DetailName => IsRussian ? "Название:" : "Name:";
        public static string DetailPublisher => IsRussian ? "Разработчик:" : "Publisher:";
        public static string DetailVersion => IsRussian ? "Версия:" : "Version:";
        public static string DetailInstallDate => IsRussian ? "Дата установки:" : "Install date:";
        public static string DetailSize => IsRussian ? "Размер (КБ):" : "Size (KB):";
        public static string DetailLocation => IsRussian ? "Установлено в:" : "Installed in:";
        public static string DetailRegistryKey => IsRussian ? "Ключ реестра:" : "Registry key:";
        public static string DetailInstallerType => IsRussian ? "Тип установщика:" : "Installer type:";
        public static string DetailUninstallCmd => IsRussian ? "Команда удаления:" : "Uninstall command:";
        public static string DetailQuietCmd => IsRussian ? "Тихая команда:" : "Quiet command:";
        public static string Unknown => IsRussian ? "неизвестно" : "unknown";
        public static string NotSpecified => IsRussian ? "не указана" : "not specified";
        public static string NotSpecifiedM => IsRussian ? "не указано" : "not specified";
        public static string NotSpecifiedSize => IsRussian ? "не указан" : "not specified";
        public static string Absent => IsRussian ? "отсутствует" : "absent";
        public static string CopyUninstallCmd => IsRussian ? "Копировать команду удаления" : "Copy uninstall command";
        public static string UseForUninstall => IsRussian ? "Использовать для удаления" : "Use for uninstall";
        public static string DeleteRegistryEntry => IsRussian ? "Удалить запись в реестре" : "Delete registry entry";
        public static string UninstallCmdCopied => IsRussian ? "Команда удаления скопирована в буфер обмена." : "Uninstall command copied to clipboard.";
        public static string UninstallCmdNotFound => IsRussian ? "Команда удаления не найдена." : "Uninstall command not found.";
        public static string ConfirmDeleteRegistry(string name) =>
            IsRussian ?
            $"Вы уверены, что хотите удалить запись реестра для \"{name}\"?\nЭто не удалит файлы программы, но уберёт её из списка установленных." :
            $"Are you sure you want to delete the registry entry for \"{name}\"?\nThis will not remove the program files, but will remove it from the list of installed programs.";
        public static string RegistryDeleted => IsRussian ? "Запись успешно удалена." : "Registry entry successfully deleted.";
        public static string RegistryDeleteFailed => IsRussian ? "Не удалось удалить запись. Возможно, недостаточно прав." : "Could not delete registry entry. Possibly insufficient permissions.";
        public static string FileNotFoundDialog(string path) =>
            IsRussian ?
            $"Файл деинсталлятора не существует:\n{path}\n\nПрограмма, возможно, уже удалена. Удалить запись из реестра?" :
            $"The uninstaller file does not exist:\n{path}\n\nThe program may already have been removed. Delete registry entry?";

        // Обновления
        public static string UpdateAvailable(string latest, string current) =>
            IsRussian ?
            $"Доступна новая версия: v{latest}\nТекущая версия: v{current}\n\nЗагрузить и установить обновление автоматически?" :
            $"New version available: v{latest}\nCurrent version: v{current}\n\nDownload and install the update automatically?";
        public static string UpdateManual(string latest, string current) =>
            IsRussian ?
            $"Доступна новая версия: v{latest}\nТекущая версия: v{current}\n\nНе удалось найти ссылку для автоматической загрузки. Открыть страницу релиза вручную?" :
            $"New version available: v{latest}\nCurrent version: v{current}\n\nCould not find automatic download link. Open the release page manually?";
        public static string UpdateChecking => IsRussian ? "[Обновление] Проверка наличия новой версии..." : "[Update] Checking for new version...";
        public static string UpdateResponseReceived => IsRussian ? "[Обновление] Ответ от GitHub получен." : "[Update] GitHub response received.";
        public static string UpdateTagNotFound => IsRussian ? "[Обновление] Не удалось извлечь tag_name из ответа." : "[Update] Could not extract tag_name from response.";
        public static string UpdateVersionParseFailed(string version) => IsRussian
            ? $"[Обновление] Не удалось распознать версию: '{version}'"
            : $"[Update] Could not parse version: '{version}'";
        public static string UpdateCurrentVersionFailed => IsRussian ? "[Обновление] Не удалось получить текущую версию приложения." : "[Update] Could not get current application version.";
        public static string UpdateVersionInfo(string current, string latest) => IsRussian
            ? $"[Обновление] Текущая версия: {current}, доступная: {latest}"
            : $"[Update] Current version: {current}, available: {latest}";
        public static string UpdateNoNewVersion => IsRussian ? "[Обновление] Нет новой версии." : "[Update] No new version.";
        public static string UpdateNewVersionFound => IsRussian ? "[Обновление] Обнаружена новая версия!" : "[Update] New version found!";
        public static string UpdateDownloadUrlNotFound => IsRussian ? "[Обновление] Не удалось найти ссылку для загрузки." : "[Update] Could not find download link.";
        public static string UpdateDownloading => IsRussian ? "[Обновление] Загрузка новой версии..." : "[Update] Downloading new version...";
        public static string UpdateDownloadComplete => IsRussian ? "[Обновление] Загрузка завершена. Установка обновления..." : "[Update] Download complete. Installing update...";
        public static string UpdateError(string message) => IsRussian ? $"[Обновление] Ошибка: {message}" : $"[Update] Error: {message}";
        public static string UpdateInstallError(string message) => IsRussian ? $"[Обновление] Ошибка при загрузке/установке: {message}" : $"[Update] Download/install error: {message}";
        public static string UpdateAutoFailed(string message) => IsRussian
            ? $"Не удалось выполнить автоматическое обновление: {message}\r\nВы можете обновиться вручную, скачав новую версию с GitHub."
            : $"Automatic update failed: {message}\r\nYou can update manually by downloading the new version from GitHub.";

        // Логи
        public static string LogStart => IsRussian ? "Загрузка списка установленных приложений..." : "Loading installed applications...";
        public static string LogLoaded => IsRussian ? "Загружено {0} приложений." : "Loaded {0} applications.";
        public static string LogLoadError(string message) => IsRussian ? $"Ошибка загрузки списка: {message}" : $"Error loading list: {message}";
        public static string ProductNotFound => IsRussian ? "Продукт не найден в реестре." : "Product not found in registry.";
        public static string LogSelectedProduct(string name) => string.Format(IsRussian ? "Выбран продукт: {0}" : "Selected product: {0}", name);
        public static string LogSelectedForUninstall(string name) => string.Format(IsRussian ? "Продукт '{0}' выбран для удаления." : "Product '{0}' selected for uninstall.", name);
        public static string LogUninstallStartDirect => IsRussian ? "Начало операции (прямой запуск)." : "Starting operation (direct launch).";
        public static string LogUninstallStartService => IsRussian ? "Начало операции (через службу)." : "Starting operation (via service).";
        public static string LogUninstallStartSystem => IsRussian ? "Начало операции (SYSTEM-запуск)." : "Starting operation (SYSTEM launch).";
        public static string LogProcessStarted(int pid) => string.Format(IsRussian ? "Процесс запущен (PID: {0})." : "Process started (PID: {0}).", pid);
        public static string LogProcessFinished => IsRussian ? "Процесс деинсталляции завершён." : "Uninstall process finished.";
        public static string LogTimeout => IsRussian ? "Достигнут таймаут." : "Timeout reached.";
        public static string LogOperationStopped => IsRussian ? "Запрошена остановка операции. Пожалуйста, подождите..." : "Stop requested. Please wait...";
        public static string LogWaitInterrupted => IsRussian ? "Ожидание прервано пользователем." : "Wait interrupted by user.";
        public static string LogRegistryEntryRemoved => IsRussian ? "Запись реестра успешно удалена." : "Registry entry successfully removed.";
        public static string LogRegistryEntryRemain => IsRussian ? "Запись реестра не удалена. Возможно, потребуется удалить её вручную через детали программы." : "Registry entry not removed. You may need to delete it manually via program details.";
        public static string LogServiceError(string name, string message) => string.Format(IsRussian ? "Ошибка при запуске службы '{0}': {1}" : "Error starting service '{0}': {1}", name, message);
        public static string LogCustomCommand(string cmd) => string.Format(IsRussian ? "Пользовательская команда: {0}" : "Custom command: {0}", cmd);
        public static string LogProductNotFound(string name) => string.Format(IsRussian ? "Продукт '{0}' не найден в реестре." : "Product '{0}' not found in registry.", name);
        public static string LogKeyFound(string key) => string.Format(IsRussian ? "Найден ключ: {0}" : "Found key: {0}", key);
        public static string LogDisplayName(string name) => string.Format(IsRussian ? "DisplayName: {0}" : "DisplayName: {0}", name);
        public static string LogInstallerType(string type) => string.Format(IsRussian ? "Тип установщика: {0}" : "Installer type: {0}", type);
        public static string LogUsingQuietUninstall => IsRussian ? "Используем QuietUninstallString" : "Using QuietUninstallString";
        public static string LogUninstallStringEmpty => IsRussian ? "UninstallString пустая." : "UninstallString is empty.";
        public static string LogFinalCommand(string cmd) => string.Format(IsRussian ? "Итоговая команда: {0}" : "Final command: {0}", cmd);
        public static string LogRegistryDeleteFailed(string key, string path, string message) => string.Format(IsRussian ? "Не удалось удалить ключ {0} из {1}: {2}" : "Could not delete key {0} from {1}: {2}", key, path, message);
        public static string LogNetworkDisabling => IsRussian ? "Отключение сетевых адаптеров..." : "Disabling network adapters...";
        public static string LogNetworkAdapterDisabled(string name) => string.Format(IsRussian ? "Отключен: {0}" : "Disabled: {0}", name);
        public static string LogNetworkDisabled => IsRussian ? "Сеть успешно отключена." : "Network successfully disabled.";
        public static string LogNetworkDisableError(string message) => string.Format(IsRussian ? "Ошибка отключения сети: {0}" : "Network disable error: {0}", message);
        public static string LogNetworkEnabling => IsRussian ? "Включение сетевых адаптеров..." : "Enabling network adapters...";
        public static string LogNetworkAdapterEnabled(string name) => string.Format(IsRussian ? "Включен: {0}" : "Enabled: {0}", name);
        public static string LogNetworkEnabled => IsRussian ? "Сеть успешно восстановлена." : "Network successfully restored.";
        public static string LogNetworkEnableError(string message) => string.Format(IsRussian ? "Ошибка включения сети: {0}" : "Network enable error: {0}", message);
        public static string LogUninstallerFileNotFound(string path) => string.Format(IsRussian ? "Файл деинсталлятора не найден: {0}" : "Uninstaller file not found: {0}", path);
        public static string LogLaunching(string file, string args) => string.Format(IsRussian ? "Запускаем: {0} {1}" : "Launching: {0} {1}", file, args);
        public static string LogServiceNotFound(string name) => string.Format(IsRussian ? "Служба '{0}' не найдена." : "Service '{0}' not found.", name);
        public static string LogServiceAlreadyModified => IsRussian ? "Служба уже изменена. Попытка восстановить из бэкапа." : "Service already modified. Attempting restore from backup.";
        public static string LogServiceRestoredFromBackup(string path) => string.Format(IsRussian ? "Восстановлен оригинальный ImagePath из бэкапа: {0}" : "Restored original ImagePath from backup: {0}", path);
        public static string LogServiceBackupNotFound => IsRussian ? "Бэкап не найден. Невозможно восстановить оригинальный путь. Используйте прямой запуск." : "Backup not found. Cannot restore original path. Use direct launch.";
        public static string LogOriginalImagePath(string path) => string.Format(IsRussian ? "Оригинальный ImagePath: {0}" : "Original ImagePath: {0}", path);
        public static string LogSettingImagePath(string path) => string.Format(IsRussian ? "Устанавливаем ImagePath = {0}" : "Setting ImagePath = {0}", path);
        public static string LogStoppingService(string name) => string.Format(IsRussian ? "Останавливаем службу '{0}'..." : "Stopping service '{0}'...", name);
        public static string LogStartingService(string name) => string.Format(IsRussian ? "Запускаем службу '{0}' — выполнение команды начато." : "Starting service '{0}' — command execution started.", name);
        public static string LogServiceStartWarning => IsRussian ? "Служба не запустилась (ошибка 1053?), но команда могла быть отправлена." : "Service did not start (error 1053?), but command may have been sent.";
        public static string LogRestoringImagePath(string path) => string.Format(IsRussian ? "Восстанавливаем оригинальный ImagePath: {0}" : "Restoring original ImagePath: {0}", path);
        public static string LogRestartingService(string name) => string.Format(IsRussian ? "Перезапускаем службу '{0}'..." : "Restarting service '{0}'...", name);
        public static string LogServiceRestartFailed => IsRussian ? "Не удалось перезапустить службу." : "Could not restart service.";
        public static string LogServiceRestored => IsRussian ? "Операция завершена. Служба восстановлена." : "Operation complete. Service restored.";
        public static string LogForceRestoreBackup => IsRussian ? "Принудительное восстановление из бэкапа в finally." : "Forced restore from backup in finally.";
        public static string LogSystemLaunch(string cmd) => string.Format(IsRussian ? "Запуск от SYSTEM: {0}" : "Launch as SYSTEM: {0}", cmd);
        public static string LogSystemLaunchFailed(int code) => string.Format(IsRussian ? "Не удалось запустить процесс от SYSTEM. Код ошибки: {0}" : "Could not launch process as SYSTEM. Error code: {0}", code);
        public static string LogProcessStartedName(string name) => string.Format(IsRussian ? "Процесс {0} запущен." : "Process {0} started.", name);
        public static string LogProcessNotFound(string name) => string.Format(IsRussian ? "Процесс {0} не обнаружен." : "Process {0} not detected.", name);
        public static string LogWaitUninstall(int maxWait) => string.Format(IsRussian ? "Ожидание завершения деинсталляции (макс. {0} сек)..." : "Waiting for uninstall to complete (max {0} sec)...", maxWait);
        public static string LogProductRemoved(string name) => string.Format(IsRussian ? "Продукт '{0}' успешно удалён!" : "Product '{0}' successfully removed!", name);
        public static string LogProductStillPresent => IsRussian ? "Продукт ещё присутствует..." : "Product still present...";
        public static string LogCustomCommandWait(int maxWait) => string.Format(IsRussian ? "Пользовательская команда запущена. Ожидание {0} секунд..." : "Custom command launched. Waiting {0} seconds...", maxWait);
        public static string LogWaitComplete => IsRussian ? "Ожидание завершено." : "Wait complete.";
        public static string ServiceRegistryOpenFailed(string name) => string.Format(IsRussian ? "Не удалось открыть раздел реестра для {0}" : "Could not open registry key for {0}", name);

        public static string SizeGb(double value) => IsRussian ? $"{value:F2} ГБ" : $"{value:F2} GB";
        public static string SizeMb(double value) => IsRussian ? $"{value:F2} МБ" : $"{value:F2} MB";
        public static string SizeKb(int kb) => IsRussian ? $"{kb} КБ" : $"{kb} KB";
    }
}
