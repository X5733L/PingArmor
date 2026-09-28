namespace PingArmor.Localization;

public class LocalizedStrings
{
    public required string AppTitle { get; init; }
    public required string HeaderAdmin { get; init; }
    public required string HeaderNoAdmin { get; init; }
    public required string OptimizeNow { get; init; }
    public required string PauseProtection { get; init; }
    public required string ResumeProtection { get; init; }
    public required string StartupWithWindows { get; init; }
    public required string GamingMode { get; init; }
    public required string Notifications { get; init; }
    public required string LanguageSubmenu { get; init; }
    public required string EventLog { get; init; }
    public required string RestartAsAdmin { get; init; }
    public required string Exit { get; init; }

    public required string StatusInitializing { get; init; }
    public required string StatusProtected { get; init; }
    public required string StatusAdjusting { get; init; }
    public required string StatusPaused { get; init; }
    public required string StatusNoConnection { get; init; }
    public required string PrimaryChannelDetecting { get; init; }
    public required string PrimaryChannelFormat { get; init; }
    public required string PrimaryChannelNone { get; init; }
    public required string TrayTextNoConnection { get; init; }

    public required string BalloonOptimizedFormat { get; init; }
    public required string GamingModeEnabledToast { get; init; }
    public required string GamingModeDisabledToast { get; init; }
    public required string NotificationsToggledFormat { get; init; }
    public required string NotificationsEnabled { get; init; }
    public required string NotificationsDisabled { get; init; }

    public required string LogWindowTitle { get; init; }
    public required string Clear { get; init; }
    public required string LogOpened { get; init; }

    public required string StartupTaskSuccess { get; init; }
    public required string StartupTaskFail { get; init; }
    public required string StartupTaskRemoveSuccess { get; init; }
    public required string StartupTaskRemoveFail { get; init; }

    public required string AlreadyRunning { get; init; }
    public required string CliOptimizing { get; init; }
    public required string CliStatusHeader { get; init; }
    public required string CliAnalysisResultFormat { get; init; }
    public required string CliGamingOn { get; init; }
    public required string CliGamingOff { get; init; }
    public required string CliUsage { get; init; }
    public required string CliHelpRunTray { get; init; }
    public required string CliHelpOptimize { get; init; }
    public required string CliHelpStatus { get; init; }
    public required string CliHelpInstallStartup { get; init; }
    public required string CliHelpUninstallStartup { get; init; }
    public required string CliHelpGamingOn { get; init; }
    public required string CliHelpGamingOff { get; init; }
    public required string CliHelpLang { get; init; }
    public required string CliHelpRestore { get; init; }
    public required string CliRestoring { get; init; }
    public required string CliRestoreComplete { get; init; }

    // Backup & Restore UI strings
    public required string RestoreSettings { get; init; }
    public required string RestoreNoBackup { get; init; }
    public required string RestoreConfirmMessage { get; init; }
    public required string RestoreStarting { get; init; }
    public required string RestoreComplete { get; init; }
    public required string RestoreOnExit { get; init; }
    public required string CliHelpBackup { get; init; }
    public required string CliBackupCreating { get; init; }
    public required string CliBackupComplete { get; init; }
    public required string BackupCreatedToast { get; init; }

    // Dashboard & Fluent UI strings
    public required string OpenDashboard { get; init; }
    public required string NavOverview { get; init; }
    public required string NavAdapters { get; init; }
    public required string NavTuning { get; init; }
    public required string NavRollback { get; init; }
    public required string NavLog { get; init; }

    public required string DashboardTitle { get; init; }
    public required string HeroCardTitle { get; init; }
    public required string ActiveChannelTitle { get; init; }
    public required string MetricOptimizationTitle { get; init; }
    public required string MetricOptimizationDesc { get; init; }

    public required string WlanOptimizerDesc { get; init; }
    public required string StartupDesc { get; init; }
    public required string RestoreOnExitDesc { get; init; }
    public required string NotificationsDesc { get; init; }
    public required string DisableIPv6OnWifiTitle { get; init; }
    public required string DisableIPv6OnWifiDesc { get; init; }
    public required string DisableSmartDnsTitle { get; init; }
    public required string DisableSmartDnsDesc { get; init; }
    public required string DisableWpadTitle { get; init; }
    public required string DisableWpadDesc { get; init; }
    public required string FlushDnsTitle { get; init; }
    public required string FlushDnsDesc { get; init; }
    public required string SettingsGroupNetwork { get; init; }
    public required string SettingsGroupApp { get; init; }

    public required string RollbackBackupCardTitle { get; init; }
    public required string RollbackBackupStatusFound { get; init; }
    public required string RollbackBackupStatusNotFound { get; init; }
    public required string RollbackBackupDesc { get; init; }
    public required string RollbackWindowsResetTitle { get; init; }
    public required string RollbackWindowsResetDesc { get; init; }
    public required string BtnWindowsReset { get; init; }
    public required string BtnOpenWindowsSettings { get; init; }
    public required string ResetConfirmTitle { get; init; }
    public required string ResetConfirmMessage { get; init; }
    public required string ResetCompletedMessage { get; init; }

    public required string AdaptersHeaderName { get; init; }
    public required string AdaptersHeaderType { get; init; }
    public required string AdaptersHeaderStatus { get; init; }
    public required string AdaptersHeaderMetric { get; init; }
    public required string AdaptersHeaderInternet { get; init; }
    public required string AdaptersHeaderExclude { get; init; }
    public required string AdaptersExcludedTag { get; init; }
    public required string AdaptersActiveTag { get; init; }
    public required string AdaptersDisconnectedTag { get; init; }

    public required string CopyLog { get; init; }
    public required string LogOpenFile { get; init; }
    public required string LogCopiedToast { get; init; }
    public required string LanguageInterfaceTitle { get; init; }
    public required string LanguageInterfaceDesc { get; init; }
    public required string ThemeTitle { get; init; }
    public required string ThemeDesc { get; init; }
    public required string ThemeSystem { get; init; }
    public required string ThemeLight { get; init; }
    public required string ThemeDark { get; init; }
    public required string OptimizeNowShort { get; init; }
    public required string AdaptersRefresh { get; init; }
    public required string AboutIconCredit { get; init; }
    public required string OverviewPausedDesc { get; init; }
    public required string WlanOptimizerActive { get; init; }
    public required string WlanOptimizerStandard { get; init; }
    public required string InternetYes { get; init; }
    public required string InternetNo { get; init; }
    public required string PrimaryAdapterDetailsFormat { get; init; }
    public required string AdapterTypeEthernet { get; init; }
    public required string AdapterTypeWiFi { get; init; }
    public required string AdapterTypeVirtual { get; init; }
    public required string AdapterTypeOther { get; init; }
    public required string AdaptersGroupPhysical { get; init; }
    public required string AdaptersGroupVirtual { get; init; }
    public required string AdaptersEmpty { get; init; }
    public required string AdaptersSearchPlaceholder { get; init; }

    public static readonly LocalizedStrings Ru = new()
    {
        AppTitle = "PingArmor",
        HeaderAdmin = "PingArmor (Админ)",
        HeaderNoAdmin = "PingArmor (Без прав админа!)",
        OptimizeNow = "Оптимизировать приоритеты сети",
        PauseProtection = "Приостановить защиту",
        ResumeProtection = "Возобновить защиту",
        StartupWithWindows = "Автозапуск при входе в Windows",
        GamingMode = "Отключение фонового поиска Wi-Fi",
        Notifications = "Всплывающие уведомления",
        LanguageSubmenu = "Язык",
        EventLog = "Журнал событий (Лог)",
        RestartAsAdmin = "Перезапустить с правами Администратора",
        Exit = "Выход",

        StatusInitializing = "Статус: Инициализация...",
        StatusProtected = "Статус: Защищено (Приоритет в норме)",
        StatusAdjusting = "Статус: Корректировка метрик...",
        StatusPaused = "Статус: Приостановлено",
        StatusNoConnection = "Статус: Нет подключения к сети",
        PrimaryChannelDetecting = "Основной канал: Определение...",
        PrimaryChannelFormat = "Основной: {0} (мет: {1})",
        PrimaryChannelNone = "Основной: Нет активного адаптера",
        TrayTextNoConnection = "PingArmor: Нет подключения",

        BalloonOptimizedFormat = "Сетевой приоритет восстановлен! Обновлено адаптеров: {0}. DNS оптимизирован.",
        GamingModeEnabledToast = "Фоновый поиск сетей Wi-Fi отключен (устранены скачки пинга).",
        GamingModeDisabledToast = "Стандартный поиск сетей Wi-Fi восстановлен.",
        NotificationsToggledFormat = "[*] Toast notifications: {0}",
        NotificationsEnabled = "Включены",
        NotificationsDisabled = "Отключены",

        LogWindowTitle = "PingArmor - Журнал событий и статус",
        Clear = "Очистить",
        LogOpened = "[*] Event log opened. Current status is active.",

        StartupTaskSuccess = "[+] Autostart via Task Scheduler successfully enabled.",
        StartupTaskFail = "[-] Failed to enable autostart.",
        StartupTaskRemoveSuccess = "[*] Autostart disabled.",
        StartupTaskRemoveFail = "[-] Failed to disable autostart.",

        AlreadyRunning = "PingArmor уже запущен в фоновом режиме. Проверьте системный трей возле часов.",
        CliOptimizing = "=== Выполнение оптимизации сетевых приоритетов... ===",
        CliStatusHeader = "=== Текущие сетевые адаптеры и метрики ===",
        CliAnalysisResultFormat = "Результат анализа: {0}",
        CliGamingOn = "=== Отключение фонового поиска сетей Wi-Fi ===",
        CliGamingOff = "=== Восстановление стандартного поиска сетей Wi-Fi ===",
        CliUsage = "Использование:",
        CliHelpRunTray = "Запуск в фоновом режиме (системный трей)",
        CliHelpOptimize = "Оптимизировать сетевые приоритеты и DNS",
        CliHelpStatus = "Показать список адаптеров и метрик",
        CliHelpInstallStartup = "Включить автозапуск при входе в систему",
        CliHelpUninstallStartup = "Отключить автозапуск",
        CliHelpGamingOn = "Отключить фоновый поиск сетей Wi-Fi",
        CliHelpGamingOff = "Включить стандартный поиск сетей Wi-Fi",
        CliHelpLang = "Установить язык (ru, en, kk)",
        CliHelpRestore = "Восстановить исходные сетевые настройки из бэкапа",
        CliRestoring = "=== Восстановление исходных сетевых настроек... ===",
        CliRestoreComplete = "=== Восстановление завершено. ===",
        RestoreSettings = "Восстановить исходные настройки",
        RestoreNoBackup = "Файл бэкапа не найден. Восстановление невозможно.",
        RestoreConfirmMessage = "Вы уверены, что хотите восстановить исходные сетевые настройки?\nВсе изменения, внесённые PingArmor, будут отменены.",
        RestoreStarting = "[*] Восстановление исходных сетевых настроек...",
        RestoreComplete = "[+] Исходные сетевые настройки успешно восстановлены.",
        RestoreOnExit = "Безопасный выход (откат настроек при закрытии)",
        CliHelpBackup = "Создать резервную копию текущих сетевых настроек",
        CliBackupCreating = "=== Создание резервной копии настроек сети... ===",
        CliBackupComplete = "=== Резервная копия успешно создана (backups/backup.json). ===",
        BackupCreatedToast = "Резервная копия исходных настроек создана (backups/backup.json).",

        OpenDashboard = "Панель управления",
        NavOverview = "Главная",
        NavAdapters = "Сетевые адаптеры",
        NavTuning = "Параметры",
        NavRollback = "Откат и сброс",
        NavLog = "Журнал событий",

        DashboardTitle = "PingArmor - Панель управления",
        HeroCardTitle = "Состояние сетевого приоритета",
        ActiveChannelTitle = "Основной интернет-канал",
        MetricOptimizationTitle = "Оптимизировать приоритеты сети",
        MetricOptimizationDesc = "Назначает высший приоритет основному каналу и снижает метрику виртуальных/VPN адаптеров.",

        WlanOptimizerDesc = "Блокирует фоновый поиск сетей Windows во время игры (устраняет скачки пинга).",
        StartupDesc = "Автоматический запуск PingArmor в фоновом режиме при старте Windows.",
        RestoreOnExitDesc = "При выходе из PingArmor возвращает сетевые метрики и политики в исходное состояние.",
        NotificationsDesc = "Показ всплывающих сообщений при изменении приоритетов и состояния адаптеров.",
        DisableIPv6OnWifiTitle = "Отключение IPv6 на адаптерах Wi-Fi",
        DisableIPv6OnWifiDesc = "Устраняет задержки dual-stack fallback и предотвращает утечки DNS на Wi-Fi.",
        DisableSmartDnsTitle = "Отключение Smart Name Resolution (DNS)",
        DisableSmartDnsDesc = "Запрещает отправку параллельных DNS-запросов во все интерфейсы сразу.",
        DisableWpadTitle = "Отключение автоопределения прокси (WPAD)",
        DisableWpadDesc = "Устраняет сетевые задержки при поиске прокси-серверов в локальной сети.",
        FlushDnsTitle = "Автоматическая очистка кэша DNS",
        FlushDnsDesc = "Сбрасывает системный кэш DNS при смене маршрутов для мгновенного обновления.",
        SettingsGroupNetwork = "Сетевые настройки и оптимизация Windows",
        SettingsGroupApp = "Настройки программы PingArmor",

        RollbackBackupCardTitle = "Резервная копия PingArmor",
        RollbackBackupStatusFound = "Снимок создан: {0} ({1} адапт.)",
        RollbackBackupStatusNotFound = "Снимок еще не создан",
        RollbackBackupDesc = "Мгновенный откат всех параметров, измененных PingArmor, в исходное состояние без перезагрузки.",
        RollbackWindowsResetTitle = "Сброс сети Windows",
        RollbackWindowsResetDesc = "Открывает «Дополнительные сетевые параметры». В открывшемся окне прокрутите вниз до блока «Дополнительные параметры» и выберите «Сброс сети» → «Сбросить сейчас» (переустановит адаптеры и перезагрузит ПК).",
        BtnWindowsReset = "Сбросить сетевой стек Windows",
        BtnOpenWindowsSettings = "Открыть сброс сети в Параметрах Windows",
        LanguageInterfaceTitle = "Язык интерфейса",
        LanguageInterfaceDesc = "Выберите язык приложения PingArmor (RU, EN, KK)",
        ThemeTitle = "Оформление",
        ThemeDesc = "Тема интерфейса: системная, светлая или тёмная",
        ThemeSystem = "Системная",
        ThemeLight = "Светлая",
        ThemeDark = "Тёмная",
        OptimizeNowShort = "Оптимизировать",
        AdaptersRefresh = "Обновить список",
        AboutIconCredit = "Иконка приложения: Magnific (Flaticon)",
        OverviewPausedDesc = "Мониторинг сети приостановлен пользователем.",
        WlanOptimizerActive = "Фоновый поиск Wi-Fi: отключён (устранение скачков пинга)",
        WlanOptimizerStandard = "Фоновый поиск Wi-Fi: стандартный режим Windows",
        InternetYes = "Есть",
        InternetNo = "Нет",
        PrimaryAdapterDetailsFormat = "Тип: {0} • Метрика IPv4: {1} • Интернет: {2}",
        AdapterTypeEthernet = "Ethernet",
        AdapterTypeWiFi = "Wi-Fi",
        AdapterTypeVirtual = "Виртуальный / VPN",
        AdapterTypeOther = "Прочее",
        AdaptersGroupPhysical = "Физические адаптеры",
        AdaptersGroupVirtual = "Виртуальные и VPN",
        AdaptersEmpty = "Сетевые адаптеры не найдены",
        AdaptersSearchPlaceholder = "Поиск по адаптерам",
        ResetConfirmTitle = "Подтверждение сброса",
        ResetConfirmMessage = "Вы действительно хотите сбросить сетевой стек Windows (Winsock, TCP/IP, AutomaticMetric)?\nВсе сетевые настройки вернутся к заводским значениям.",
        ResetCompletedMessage = "Сетевой стек Windows успешно сброшен к заводским параметрам. Рекомендуется перезагрузить компьютер.",

        AdaptersHeaderName = "Адаптер",
        AdaptersHeaderType = "Тип",
        AdaptersHeaderStatus = "Состояние",
        AdaptersHeaderMetric = "Метрика",
        AdaptersHeaderInternet = "Интернет",
        AdaptersHeaderExclude = "Исключить",
        AdaptersExcludedTag = "Исключен",
        AdaptersActiveTag = "Подключен",
        AdaptersDisconnectedTag = "Отключен",

        CopyLog = "Копировать",
        LogOpenFile = "Файл лога",
        LogCopiedToast = "Журнал событий скопирован в буфер обмена."
    };

    public static readonly LocalizedStrings En = new()
    {
        AppTitle = "PingArmor",
        HeaderAdmin = "PingArmor (Admin)",
        HeaderNoAdmin = "PingArmor (No Admin Rights!)",
        OptimizeNow = "Optimize network priorities",
        PauseProtection = "Pause protection",
        ResumeProtection = "Resume protection",
        StartupWithWindows = "Launch on Windows startup",
        GamingMode = "Disable Wi-Fi background scan",
        Notifications = "Notifications",
        LanguageSubmenu = "Language",
        EventLog = "Event log",
        RestartAsAdmin = "Restart as Administrator",
        Exit = "Exit",

        StatusInitializing = "Status: Initializing...",
        StatusProtected = "Status: Protected (Priority optimal)",
        StatusAdjusting = "Status: Adjusting metrics...",
        StatusPaused = "Status: Paused",
        StatusNoConnection = "Status: No network connection",
        PrimaryChannelDetecting = "Primary interface: Detecting...",
        PrimaryChannelFormat = "Primary: {0} (metric: {1})",
        PrimaryChannelNone = "Primary: No active adapter",
        TrayTextNoConnection = "PingArmor: No connection",

        BalloonOptimizedFormat = "Network priority restored! Updated adapters: {0}. DNS optimized.",
        GamingModeEnabledToast = "Wi-Fi background scanning disabled (latency spikes eliminated).",
        GamingModeDisabledToast = "Standard Wi-Fi scanning restored.",
        NotificationsToggledFormat = "[*] Toast notifications: {0}",
        NotificationsEnabled = "Enabled",
        NotificationsDisabled = "Disabled",

        LogWindowTitle = "PingArmor - Event Log & Status",
        Clear = "Clear",
        LogOpened = "[*] Log opened. Current status is active.",

        StartupTaskSuccess = "[+] Autostart via Task Scheduler successfully enabled.",
        StartupTaskFail = "[-] Failed to enable autostart.",
        StartupTaskRemoveSuccess = "[*] Autostart disabled.",
        StartupTaskRemoveFail = "[-] Failed to disable autostart.",

        AlreadyRunning = "PingArmor is already running in the background. Check the system tray near the clock.",
        CliOptimizing = "=== Executing network priority optimization... ===",
        CliStatusHeader = "=== Current network adapters and metrics ===",
        CliAnalysisResultFormat = "Analysis result: {0}",
        CliGamingOn = "=== Disabling Wi-Fi background scan ===",
        CliGamingOff = "=== Restoring standard Wi-Fi scan ===",
        CliUsage = "Usage:",
        CliHelpRunTray = "Run in background mode (system tray)",
        CliHelpOptimize = "Optimize network priorities and DNS",
        CliHelpStatus = "Show list of adapters and metrics",
        CliHelpInstallStartup = "Enable startup via Task Scheduler",
        CliHelpUninstallStartup = "Disable autostart",
        CliHelpGamingOn = "Disable Wi-Fi background scan",
        CliHelpGamingOff = "Enable standard Wi-Fi scan",
        CliHelpLang = "Set interface language (ru, en, kk)",
        CliHelpRestore = "Restore original network settings from backup",
        CliRestoring = "=== Restoring original network settings... ===",
        CliRestoreComplete = "=== Restoration complete. ===",
        RestoreSettings = "Restore original settings",
        RestoreNoBackup = "No backup file found. Cannot restore.",
        RestoreConfirmMessage = "Are you sure you want to restore original network settings?\nAll changes made by PingArmor will be reverted.",
        RestoreStarting = "[*] Restoring original network settings...",
        RestoreComplete = "[+] Original network settings successfully restored.",
        RestoreOnExit = "Safe exit (restore original settings on close)",
        CliHelpBackup = "Create backup of current network settings",
        CliBackupCreating = "=== Creating backup of network settings... ===",
        CliBackupComplete = "=== Backup successfully created (backups/backup.json). ===",
        BackupCreatedToast = "Backup of original settings created (backups/backup.json).",

        OpenDashboard = "Dashboard",
        NavOverview = "Overview",
        NavAdapters = "Network Adapters",
        NavTuning = "Settings",
        NavRollback = "Rollback & Reset",
        NavLog = "Event Log",

        DashboardTitle = "PingArmor - Dashboard",
        HeroCardTitle = "Network Priority Status",
        ActiveChannelTitle = "Primary Internet Channel",
        MetricOptimizationTitle = "Optimize network priorities",
        MetricOptimizationDesc = "Assigns highest priority to primary channel and lowers metrics for virtual/VPN adapters.",

        WlanOptimizerDesc = "Disables Windows background Wi-Fi scanning during games (eliminates ping spikes).",
        StartupDesc = "Automatically launch PingArmor in background on Windows startup.",
        RestoreOnExitDesc = "Reverts network metrics and policies to initial state upon exiting PingArmor.",
        NotificationsDesc = "Show toast notifications when priority or adapter status changes.",
        DisableIPv6OnWifiTitle = "Disable IPv6 on Wi-Fi adapters",
        DisableIPv6OnWifiDesc = "Eliminates dual-stack fallback delays and prevents DNS leaks on Wi-Fi.",
        DisableSmartDnsTitle = "Disable Smart Name Resolution (DNS)",
        DisableSmartDnsDesc = "Prevents Windows from sending DNS queries to all interfaces simultaneously.",
        DisableWpadTitle = "Disable WPAD Proxy Auto-Detect",
        DisableWpadDesc = "Eliminates network delays caused by probing for proxy servers on local network.",
        FlushDnsTitle = "Automatic DNS Cache Flush",
        FlushDnsDesc = "Flushes system DNS cache whenever routes change for instant updates.",
        SettingsGroupNetwork = "Windows Network & Optimization Settings",
        SettingsGroupApp = "PingArmor Application Settings",

        RollbackBackupCardTitle = "PingArmor Backup Snapshot",
        RollbackBackupStatusFound = "Snapshot created: {0} ({1} adapters)",
        RollbackBackupStatusNotFound = "No backup snapshot created yet",
        RollbackBackupDesc = "Instantly reverts all settings modified by PingArmor to original state without restarting.",
        RollbackWindowsResetTitle = "Windows Network Reset",
        RollbackWindowsResetDesc = "Opens 'Advanced network settings'. In the opened window, scroll down to 'More settings' and select 'Network reset' -> 'Reset now' (reinstalls adapters and restarts PC).",
        BtnWindowsReset = "Reset Windows Network Stack",
        BtnOpenWindowsSettings = "Open Network Reset in Windows Settings",
        LanguageInterfaceTitle = "Interface Language",
        LanguageInterfaceDesc = "Select PingArmor application language (RU, EN, KK)",
        ThemeTitle = "Appearance",
        ThemeDesc = "Interface theme: system, light or dark",
        ThemeSystem = "System",
        ThemeLight = "Light",
        ThemeDark = "Dark",
        OptimizeNowShort = "Optimize",
        AdaptersRefresh = "Refresh list",
        AboutIconCredit = "Application icon created by Magnific (Flaticon)",
        OverviewPausedDesc = "Network monitoring was paused by the user.",
        WlanOptimizerActive = "Wi-Fi background scan: disabled (latency spikes suppressed)",
        WlanOptimizerStandard = "Wi-Fi background scan: Windows default mode",
        InternetYes = "Available",
        InternetNo = "Unavailable",
        PrimaryAdapterDetailsFormat = "Type: {0} • IPv4 metric: {1} • Internet: {2}",
        AdapterTypeEthernet = "Ethernet",
        AdapterTypeWiFi = "Wi-Fi",
        AdapterTypeVirtual = "Virtual / VPN",
        AdapterTypeOther = "Other",
        AdaptersGroupPhysical = "Physical adapters",
        AdaptersGroupVirtual = "Virtual & VPN",
        AdaptersEmpty = "No network adapters found",
        AdaptersSearchPlaceholder = "Search adapters",
        ResetConfirmTitle = "Confirm Reset",
        ResetConfirmMessage = "Are you sure you want to reset the Windows network stack (Winsock, TCP/IP, AutomaticMetric)?\nAll network interfaces will revert to factory defaults.",
        ResetCompletedMessage = "Windows network stack was successfully reset to factory defaults. A system restart is recommended.",

        AdaptersHeaderName = "Adapter",
        AdaptersHeaderType = "Type",
        AdaptersHeaderStatus = "Status",
        AdaptersHeaderMetric = "Metric",
        AdaptersHeaderInternet = "Internet",
        AdaptersHeaderExclude = "Exclude",
        AdaptersExcludedTag = "Excluded",
        AdaptersActiveTag = "Connected",
        AdaptersDisconnectedTag = "Disconnected",

        CopyLog = "Copy",
        LogOpenFile = "Log File",
        LogCopiedToast = "Event log copied to clipboard."
    };

    public static readonly LocalizedStrings Kk = new()
    {
        AppTitle = "PingArmor",
        HeaderAdmin = "PingArmor (Әкімші)",
        HeaderNoAdmin = "PingArmor (Әкімші құқығы жоқ!)",
        OptimizeNow = "Желі басымдықтарын оңтайландыру",
        PauseProtection = "Қорғауды кідірту",
        ResumeProtection = "Қорғауды жалғастыру",
        StartupWithWindows = "Windows іске қосылғанда ашылу",
        GamingMode = "Wi-Fi желілерін фонда іздеуді өшіру",
        Notifications = "Қалқымалы хабарландырулар",
        LanguageSubmenu = "Тіл",
        EventLog = "Оқиғалар журналы",
        RestartAsAdmin = "Әкімші құқығымен қайта қосу",
        Exit = "Шығу",

        StatusInitializing = "Күйі: Бапталуда...",
        StatusProtected = "Күйі: Қорғалған (Басымдық қалыпты)",
        StatusAdjusting = "Күйі: Метрикаларды реттеу...",
        StatusPaused = "Күйі: Кідіртілді",
        StatusNoConnection = "Күйі: Желіге қосылым жоқ",
        PrimaryChannelDetecting = "Негізгі арна: Анықталуда...",
        PrimaryChannelFormat = "Негізгі: {0} (метрика: {1})",
        PrimaryChannelNone = "Негізгі: Белсенді адаптер жоқ",
        TrayTextNoConnection = "PingArmor: Қосылым жоқ",

        BalloonOptimizedFormat = "Желі басымдығы қалпына келтірілді! Жаңартылған адаптерлер: {0}. DNS оңтайландырылды.",
        GamingModeEnabledToast = "Wi-Fi желілерін фонда іздеу өшірілді (пинг секірулері жойылды).",
        GamingModeDisabledToast = "Стандартты Wi-Fi іздеу қалпына келтірілді.",
        NotificationsToggledFormat = "[*] Toast notifications: {0}",
        NotificationsEnabled = "Қосулы",
        NotificationsDisabled = "Өшірулі",

        LogWindowTitle = "PingArmor - Оқиғалар журналы және күйі",
        Clear = "Тазарту",
        LogOpened = "[*] Event log opened. Current status is active.",

        StartupTaskSuccess = "[+] Autostart via Task Scheduler successfully enabled.",
        StartupTaskFail = "[-] Failed to enable autostart.",
        StartupTaskRemoveSuccess = "[*] Autostart disabled.",
        StartupTaskRemoveFail = "[-] Failed to disable autostart.",

        AlreadyRunning = "PingArmor фонда іске қосылып тұр. Сағат жанындағы жүйелік трейді тексеріңіз.",
        CliOptimizing = "=== Желі басымдықтарын оңтайландыру орындалуда... ===",
        CliStatusHeader = "=== Ағымдағы желілік адаптерлер мен метрикалар ===",
        CliAnalysisResultFormat = "Талдау нәтижесі: {0}",
        CliGamingOn = "=== Wi-Fi желілерін фонда іздеуді өшіру ===",
        CliGamingOff = "=== Стандартты Wi-Fi іздеуді қалпына келтіру ===",
        CliUsage = "Қолдану тәсілі:",
        CliHelpRunTray = "Фондық режимде іске қосу (жүйелік трей)",
        CliHelpOptimize = "Желі басымдықтары мен DNS оңтайландыру",
        CliHelpStatus = "Адаптерлер мен метрикалар тізімін көрсету",
        CliHelpInstallStartup = "Task Scheduler арқылы автоіске қосуды қосу",
        CliHelpUninstallStartup = "Автоіске қосуды өшіру",
        CliHelpGamingOn = "Wi-Fi желілерін фонда іздеуді өшіру",
        CliHelpGamingOff = "Стандартты Wi-Fi іздеуді қосу",
        CliHelpLang = "Интерфейс тілін орнату (ru, en, kk)",
        CliHelpRestore = "Бастапқы желі параметрлерін сақтық көшірмеден қалпына келтіру",
        CliRestoring = "=== Бастапқы желі параметрлерін қалпына келтіру... ===",
        CliRestoreComplete = "=== Қалпына келтіру аяқталды. ===",
        RestoreSettings = "Бастапқы параметрлерді қалпына келтіру",
        RestoreNoBackup = "Сақтық көшірме файлы табылмады. Қалпына келтіру мүмкін емес.",
        RestoreConfirmMessage = "Бастапқы желі параметрлерін қалпына келтіргіңіз келе ме?\nPingArmor жасаған барлық өзгерістер кері қайтарылады.",
        RestoreStarting = "[*] Бастапқы желі параметрлерін қалпына келтіру...",
        RestoreComplete = "[+] Бастапқы желі параметрлері сәтті қалпына келтірілді.",
        RestoreOnExit = "Қауіпсіз шығу (жабылғанда бастапқы параметрлерді қайтару)",
        CliHelpBackup = "Ағымдағы желі параметрлерінің сақтық көшірмесін жасау",
        CliBackupCreating = "=== Желі параметрлерінің сақтық көшірмесі жасалуда... ===",
        CliBackupComplete = "=== Сақтық көшірме сәтті жасалды (backups/backup.json). ===",
        BackupCreatedToast = "Бастапқы параметрлердің сақтық көшірмесі жасалды (backups/backup.json).",

        OpenDashboard = "Басқару тақтасы",
        NavOverview = "Басты бет",
        NavAdapters = "Желілік адаптерлер",
        NavTuning = "Параметрлер",
        NavRollback = "Қайтару және тастау",
        NavLog = "Оқиғалар журналы",

        DashboardTitle = "PingArmor - Басқару тақтасы",
        HeroCardTitle = "Желі басымдығының күйі",
        ActiveChannelTitle = "Негізгі интернет арнасы",
        MetricOptimizationTitle = "Желі басымдықтарын оңтайландыру",
        MetricOptimizationDesc = "Негізгі арнаға жоғары басымдық беріп, виртуалды/VPN адаптерлерінің метрикасын төмендетеді.",

        WlanOptimizerDesc = "Ойын кезінде Windows-тың фонда Wi-Fi іздеуін блоктайды (пинг секірулерін жояды).",
        StartupDesc = "Windows іске қосылғанда PingArmor-ды фонда автоматты түрде қосу.",
        RestoreOnExitDesc = "PingArmor-дан шыққанда жүйелік метрикалар мен саясаттарды бастапқы күйіне қайтарады.",
        NotificationsDesc = "Басымдық немесе адаптер күйі өзгергенде қалқымалы хабарламаларды көрсету.",
        DisableIPv6OnWifiTitle = "Wi-Fi адаптерлерінде IPv6-ны өшіру",
        DisableIPv6OnWifiDesc = "Dual-stack fallback кідірістерін және Wi-Fi желісінде DNS ағуын болдырмайды.",
        DisableSmartDnsTitle = "Smart Name Resolution (DNS) өшіру",
        DisableSmartDnsDesc = "DNS сұрауларын барлық интерфейстерге бір уақытта жіберуге тыйым салады.",
        DisableWpadTitle = "WPAD проксиді автоматты анықтауды өшіру",
        DisableWpadDesc = "Жергілікті желіде прокси-серверлерді іздеуден туындайтын кідірістерді жояды.",
        FlushDnsTitle = "DNS кэшін автоматты түрде тазарту",
        FlushDnsDesc = "Маршруттар өзгерген сайын жүйелік DNS кэшін лезде жаңарту үшін тазартады.",
        SettingsGroupNetwork = "Windows желілік баптаулары мен оңтайландыру",
        SettingsGroupApp = "PingArmor бағдарламасының баптаулары",

        RollbackBackupCardTitle = "PingArmor сақтық көшірмесі",
        RollbackBackupStatusFound = "Сақтық көшірме жасалды: {0} ({1} адапт.)",
        RollbackBackupStatusNotFound = "Сақтық көшірме әлі жасалмаған",
        RollbackBackupDesc = "PingArmor өзгерткен барлық параметрлерді қайта жүктеусіз бастапқы қалпына келтіру.",
        RollbackWindowsResetTitle = "Windows желісін тастау",
        RollbackWindowsResetDesc = "«Қосымша желілік параметрлер» бөлімін ашады. Төмен қарай айналдырып, «Қосымша параметрлер» -> «Желіні қалпына келтіру» -> «Қазір тастау» тармағын таңдаңыз (адаптерлер қайта орнатылып, компьютер қайта қосылады).",
        BtnWindowsReset = "Windows желілік стекін тастау",
        BtnOpenWindowsSettings = "Windows баптауларында желіні қалпына келтіруді ашу",
        LanguageInterfaceTitle = "Интерфейс тілі",
        LanguageInterfaceDesc = "PingArmor қолданбасының тілін таңдаңыз (RU, EN, KK)",
        ThemeTitle = "Көрініс",
        ThemeDesc = "Интерфейс тақырыбы: жүйелік, ашық немесе қараңғы",
        ThemeSystem = "Жүйелік",
        ThemeLight = "Ашық",
        ThemeDark = "Қараңғы",
        OptimizeNowShort = "Оңтайландыру",
        AdaptersRefresh = "Тізімді жаңарту",
        AboutIconCredit = "Қолданба белгішесі: Magnific (Flaticon)",
        OverviewPausedDesc = "Желі мониторингі пайдаланушымен кідіртілді.",
        WlanOptimizerActive = "Wi-Fi фондық іздеуі: өшірілген (пинг секірулері жойылған)",
        WlanOptimizerStandard = "Wi-Fi фондық іздеуі: Windows әдепкі режимі",
        InternetYes = "Бар",
        InternetNo = "Жоқ",
        PrimaryAdapterDetailsFormat = "Түрі: {0} • IPv4 метрикасы: {1} • Интернет: {2}",
        AdapterTypeEthernet = "Ethernet",
        AdapterTypeWiFi = "Wi-Fi",
        AdapterTypeVirtual = "Виртуалды / VPN",
        AdapterTypeOther = "Басқа",
        AdaptersGroupPhysical = "Физикалық адаптерлер",
        AdaptersGroupVirtual = "Виртуалды және VPN",
        AdaptersEmpty = "Желілік адаптерлер табылмады",
        AdaptersSearchPlaceholder = "Адаптерлерді іздеу",
        ResetConfirmTitle = "Тастауды растау",
        ResetConfirmMessage = "Windows желілік стекін (Winsock, TCP/IP, AutomaticMetric) шынымен тастағыңыз келе ме?\nБарлық желілік баптаулар зауыттық күйге оралады.",
        ResetCompletedMessage = "Windows желілік стекі зауыттық күйге сәтті тасталды. Компьютерді қайта қосу ұсынылады.",

        AdaptersHeaderName = "Адаптер",
        AdaptersHeaderType = "Түрі",
        AdaptersHeaderStatus = "Күйі",
        AdaptersHeaderMetric = "Метрика",
        AdaptersHeaderInternet = "Интернет",
        AdaptersHeaderExclude = "Шығару",
        AdaptersExcludedTag = "Шығарылған",
        AdaptersActiveTag = "Қосылған",
        AdaptersDisconnectedTag = "Ажыратылған",

        CopyLog = "Көшіру",
        LogOpenFile = "Журнал файлы",
        LogCopiedToast = "Оқиғалар журналы алмасу буферіне көшірілді."
    };
}
