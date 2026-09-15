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

    public static readonly LocalizedStrings Ru = new()
    {
        AppTitle = "PingArmor",
        HeaderAdmin = "PingArmor (Админ)",
        HeaderNoAdmin = "PingArmor (Без прав админа!)",
        OptimizeNow = "⚡ Оптимизировать приоритеты сети",
        PauseProtection = "⏸ Приостановить защиту",
        ResumeProtection = "▶ Возобновить защиту",
        StartupWithWindows = "🚀 Автозапуск при входе в Windows",
        GamingMode = "📶 Отключение фонового поиска Wi-Fi",
        Notifications = "🔔 Всплывающие уведомления",
        LanguageSubmenu = "🌐 Язык",
        EventLog = "📋 Журнал событий (Лог)",
        RestartAsAdmin = "⚠️ Перезапустить с правами Администратора",
        Exit = "❌ Выход",

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
        RestoreSettings = "🔄 Восстановить исходные настройки",
        RestoreNoBackup = "Файл бэкапа не найден. Восстановление невозможно.",
        RestoreConfirmMessage = "Вы уверены, что хотите восстановить исходные сетевые настройки?\nВсе изменения, внесённые PingArmor, будут отменены.",
        RestoreStarting = "[*] Восстановление исходных сетевых настроек...",
        RestoreComplete = "[+] Исходные сетевые настройки успешно восстановлены.",
        RestoreOnExit = "Безопасный выход (откат настроек при закрытии)",
        CliHelpBackup = "Создать резервную копию текущих сетевых настроек",
        CliBackupCreating = "=== Создание резервной копии настроек сети... ===",
        CliBackupComplete = "=== Резервная копия успешно создана (backup.json). ===",
        BackupCreatedToast = "Резервная копия исходных настроек создана (backup.json)."
    };

    public static readonly LocalizedStrings En = new()
    {
        AppTitle = "PingArmor",
        HeaderAdmin = "PingArmor (Admin)",
        HeaderNoAdmin = "PingArmor (No Admin Rights!)",
        OptimizeNow = "⚡ Optimize network priorities",
        PauseProtection = "⏸ Pause protection",
        ResumeProtection = "▶ Resume protection",
        StartupWithWindows = "🚀 Launch on Windows startup",
        GamingMode = "📶 Disable Wi-Fi background scan",
        Notifications = "🔔 Notifications",
        LanguageSubmenu = "🌐 Language",
        EventLog = "📋 Event log",
        RestartAsAdmin = "⚠️ Restart as Administrator",
        Exit = "❌ Exit",

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
        RestoreSettings = "🔄 Restore original settings",
        RestoreNoBackup = "No backup file found. Cannot restore.",
        RestoreConfirmMessage = "Are you sure you want to restore original network settings?\nAll changes made by PingArmor will be reverted.",
        RestoreStarting = "[*] Restoring original network settings...",
        RestoreComplete = "[+] Original network settings successfully restored.",
        RestoreOnExit = "Safe exit (restore original settings on close)",
        CliHelpBackup = "Create backup of current network settings",
        CliBackupCreating = "=== Creating backup of network settings... ===",
        CliBackupComplete = "=== Backup successfully created (backup.json). ===",
        BackupCreatedToast = "Backup of original settings created (backup.json)."
    };

    public static readonly LocalizedStrings Kk = new()
    {
        AppTitle = "PingArmor",
        HeaderAdmin = "PingArmor (Әкімші)",
        HeaderNoAdmin = "PingArmor (Әкімші құқығы жоқ!)",
        OptimizeNow = "⚡ Желі басымдықтарын оңтайландыру",
        PauseProtection = "⏸ Қорғауды кідірту",
        ResumeProtection = "▶ Қорғауды жалғастыру",
        StartupWithWindows = "🚀 Windows іске қосылғанда ашылу",
        GamingMode = "📶 Wi-Fi желілерін фонда іздеуді өшіру",
        Notifications = "🔔 Қалқымалы хабарландырулар",
        LanguageSubmenu = "🌐 Тіл",
        EventLog = "📋 Оқиғалар журналы",
        RestartAsAdmin = "⚠️ Әкімші құқығымен қайта қосу",
        Exit = "❌ Шығу",

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
        RestoreSettings = "🔄 Бастапқы параметрлерді қалпына келтіру",
        RestoreNoBackup = "Сақтық көшірме файлы табылмады. Қалпына келтіру мүмкін емес.",
        RestoreConfirmMessage = "Бастапқы желі параметрлерін қалпына келтіргіңіз келе ме?\nPingArmor жасаған барлық өзгерістер кері қайтарылады.",
        RestoreStarting = "[*] Бастапқы желі параметрлерін қалпына келтіру...",
        RestoreComplete = "[+] Бастапқы желі параметрлері сәтті қалпына келтірілді.",
        RestoreOnExit = "Қауіпсіз шығу (жабылғанда бастапқы параметрлерді қайтару)",
        CliHelpBackup = "Ағымдағы желі параметрлерінің сақтық көшірмесін жасау",
        CliBackupCreating = "=== Желі параметрлерінің сақтық көшірмесі жасалуда... ===",
        CliBackupComplete = "=== Сақтық көшірме сәтті жасалды (backup.json). ===",
        BackupCreatedToast = "Бастапқы параметрлердің сақтық көшірмесі жасалды (backup.json)."
    };
}
