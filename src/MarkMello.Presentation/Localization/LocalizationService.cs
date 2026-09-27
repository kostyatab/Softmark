using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkMello.Domain;
using MarkMello.Presentation.ViewModels;

namespace MarkMello.Presentation.Localization;

public sealed class LocalizationService : ObservableObject, ILocalizationService
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["WelcomeTagline"] = "A quiet place to read Markdown.",
        ["WelcomeNewDocument"] = "New Document",
        ["WelcomeOpenFile"] = "Open File…",
        ["RecentTitle"] = "RECENT",
        ["RecentClear"] = "Clear",
        ["RecentToday"] = "today",
        ["RecentYesterday"] = "yesterday",
        ["RecentDateFormat"] = "MMM d",
        ["RecentDateWithYearFormat"] = "MMM d, yyyy",
        ["RecentFileNotFoundTitle"] = "File not found",
        ["RecentFolderNotFoundTitle"] = "Folder not found",
        ["RecentRemoveBody"] = "Remove “{0}” from Recent?",
        ["RecentRemoveConfirm"] = "Remove",
        ["RecentRemoveCancel"] = "Cancel",
        ["WelcomeDropHint"] = "or drop a .md file or a folder here",
        ["TitleBarMinimize"] = "Minimize",
        ["TitleBarMaximize"] = "Maximize",
        ["TitleBarRestore"] = "Restore",
        ["TitleBarClose"] = "Close",
        ["AppMenuTooltip"] = "Menu",
        ["EditToggleTooltip"] = "Toggle edit mode ({0})",
        ["EditDone"] = "Done",
        ["EditDoneTooltip"] = "Finish editing ({0})",
        ["EditUnsaved"] = "Unsaved",
        ["ReadingSettingsTooltip"] = "View: theme, font, size",
        ["OverlayCloseSettings"] = "Close settings",
        ["AppSettingsHeader"] = "Settings",
        ["AppMenuNewDocument"] = "New Document",
        ["AppMenuOpenFile"] = "Open File…",
        ["AppMenuOpenFolder"] = "Open Folder…",
        ["AppMenuSave"] = "Save",
        ["AppMenuSaveAs"] = "Save As…",
        ["AppMenuReload"] = "Reload from Disk",
        ["AppMenuFilesPanel"] = "Files Panel",
        ["AppMenuCloseTab"] = "Close Tab",
        ["AppMenuSettings"] = "Settings…",
        ["AppMenuCheckForUpdates"] = "Check for Updates…",
        ["AppMenuAbout"] = $"About {AppProductInfo.Name}",
        ["AboutWindowTitle"] = $"About {AppProductInfo.Name}",
        ["AboutVersion"] = "Version {0}",
        ["AboutForkAttribution"] = "Fork of MarkMello © 2026 MarkMello contributors",
        ["AppSettingsReadingHintPrefix"] = "Theme, font and text size live in the",
        ["AppSettingsReadingHintSuffix"] = "card above the document.",
        ["LanguageLabel"] = "Language",
        ["LanguageHint"] = "Shell and dialogs",
        ["LanguageSystem"] = "System",
        ["LanguageEnglish"] = "English",
        ["LanguageRussian"] = "Russian",
        ["ReadingThemeLabel"] = "Theme",
        ["ReadingThemeAuto"] = "Auto",
        ["ReadingThemeLight"] = "Light",
        ["ReadingThemeDark"] = "Dark",
        ["ReadingFontLabel"] = "Font",
        ["ReadingFontSerif"] = "Serif",
        ["ReadingFontSans"] = "Sans",
        ["ReadingFontMono"] = "Mono",
        ["ReadingSizeLabel"] = "Text size",
        ["ReadingFontSizeValue"] = "{0} px",
        ["ReadingSizeDecreaseTooltip"] = "Smaller ({0})",
        ["ReadingSizeIncreaseTooltip"] = "Larger ({0})",
        ["ReadingLineHeightLabel"] = "Line height",
        ["ReadingWidthLabel"] = "Line width",
        ["ReadingWidthNarrow"] = "Narrow",
        ["ReadingWidthMedium"] = "Medium",
        ["ReadingWidthWide"] = "Wide",
        ["WindowBorderLabel"] = "Window border",
        ["WindowBorderHint"] = "Outline the window edge",
        ["WindowBorderAuto"] = "Auto",
        ["WindowBorderOn"] = "On",
        ["WindowBorderOff"] = "Off",
        ["DocumentOutlineLabel"] = "Contents",
        ["DocumentOutlineHint"] = "Section rail at the right edge of the document",
        ["DocumentOutlineOn"] = "On",
        ["DocumentOutlineOff"] = "Off",
        ["ReadingMoreSettingsHint"] = "Language, updates, version",
        ["ReadingMoreSettingsLink"] = "Settings…",
        ["StatusWordsOne"] = "{0:N0} word",
        ["StatusWordsFew"] = "{0:N0} words",
        ["StatusWordsMany"] = "{0:N0} words",
        ["StatusReadMinutes"] = "{0} min",
        ["DragDropHint"] = "Release to open",
        ["DropFileDetails"] = "{0} will open in a new tab",
        ["DropFolderNewWindowDetails"] = "Folder {0} will open in a new window",
        ["DropFolderOpenElsewhereDetails"] = "Folder {0} is already open in another window",
        ["DropFolderThisWindowDetails"] = "Folder {0} will open in this window",
        ["DirtyPromptCancel"] = "Cancel",
        ["DirtyPromptDiscard"] = "Discard",
        ["DirtyPromptSave"] = "Save",
        ["LoadErrorOpenAnotherFile"] = "Open another file…",
        ["LoadErrorTryAgain"] = "Try again",
        ["LoadErrorDismiss"] = "close",
        ["EditorBoldTooltip"] = "Bold",
        ["EditorItalicTooltip"] = "Italic",
        ["EditorCodeTooltip"] = "Code",
        ["EditorLinkTooltip"] = "Link",
        ["EditorListTooltip"] = "List",
        ["EditorQuoteTooltip"] = "Quote",
        ["EditorProtectedImageDataMessage"] = "Embedded image data can only be edited as a whole line.",
        ["ContextCopy"] = "Copy",
        ["ContextSelectAll"] = "Select all",
        ["ContextCopyLink"] = "Copy link",
        ["ContextCopyLinks"] = "Copy links",
        ["ContextCopyTelegramMarkdown"] = "Copy selection as Telegram Markdown",
        ["CodeCopyTooltip"] = "Copy code",
        ["CodeCopiedStatus"] = "Code copied",
        ["AlertNote"] = "Note",
        ["AlertTip"] = "Tip",
        ["AlertImportant"] = "Important",
        ["AlertWarning"] = "Warning",
        ["AlertCaution"] = "Caution",
        ["ImageLoading"] = "Loading…",
        ["DiagramRenderFailed"] = "{0} diagram could not be rendered",
        ["DiagramSvgUnsupported"] = "{0} diagram rendered, but its SVG is not yet supported by the built-in viewer",
        ["DiagramEmpty"] = "The diagram came out empty. Check its syntax.",
        ["EditorSourceLabel"] = "Markdown",
        ["EditorPreviewLabel"] = "Preview",
        ["FindPlaceholder"] = "Find in document",
        ["FindPreviousTooltip"] = "Previous match ({0})",
        ["FindToggleTooltip"] = "Find in document ({0})",
        ["FindNextTooltip"] = "Next match ({0})",
        ["FindCloseTooltip"] = "Close search ({0})",
        ["FindResultCount"] = "{0} of {1}",
        ["FindNoResults"] = "No results",
        ["UpdateWindowTitle"] = $"{AppProductInfo.Name} Update",
        ["UpdateCheckingTitle"] = "Checking for updates…",
        ["UpdateCheckingMessage"] = $"Looking for a newer version of {AppProductInfo.Name} on GitHub.",
        ["UpdateAvailableTitle"] = "Version {0} is available",
        ["UpdateAvailableMessageDmg"] = $"You have {AppProductInfo.Name} {{0}}. We'll download the installer for this Mac — {{1}}.",
        ["UpdateAvailableMessageInstaller"] = $"You have {AppProductInfo.Name} {{0}}. We'll download the installer for this PC — {{1}}.",
        ["UpdateAvailableMessageAppImage"] = $"You have {AppProductInfo.Name} {{0}}. We'll download the new AppImage — {{1}}.",
        ["UpdateWhatsNew"] = "What's new in {0} ↗",
        ["UpdateDownloadingTitle"] = "Downloading {0}",
        ["UpdateDownloadCaption"] = "{0} · {1}%",
        ["UpdateDownloadedTitle"] = "Version {0} downloaded",
        ["UpdateDownloadedMessageDmg"] = $"Open the DMG and drag {AppProductInfo.Name} to Applications. Quit the app first.",
        ["UpdateDownloadedMessageInstaller"] = $"Run the installer to update {AppProductInfo.Name}. Quit the app first.",
        ["UpdateDownloadedMessageAppImage"] = "Replace your previous AppImage with the new one. Quit the app first.",
        ["UpdateOpenFailedMessage"] = "Couldn't open {0}. Open it from its folder instead.",
        ["UpdateUpToDateTitle"] = "You're up to date",
        ["UpdateUpToDateMessage"] = $"{AppProductInfo.Name} {{0}} is the newest published version.",
        ["UpdateUnavailableTitle"] = "Updates aren't available for this build",
        ["UpdateSourceNotConfiguredMessage"] = "This build has no release source to check.",
        ["UpdateUnsupportedPlatformMessage"] = "There are no ready-made builds for {0} {1}.",
        ["UpdateCheckFailedTitle"] = "Couldn't check for updates",
        ["UpdateCheckFailedMessage"] = "No connection to GitHub. Check your internet connection and try again.",
        ["UpdateCheckFailedServiceMessage"] = "GitHub didn't return a usable release. Try again later.",
        ["UpdateDownloadFailedTitle"] = "Couldn't download the update",
        ["UpdateDownloadFailedMessage"] = "The download was interrupted. Check your internet connection and try again.",
        ["UpdateDownloadFailedOtherMessage"] = "The update couldn't be downloaded. Try again later.",
        ["UpdateCancel"] = "Cancel",
        ["UpdateLater"] = "Later",
        ["UpdateDownload"] = "Download",
        ["UpdateOpenDmg"] = "Open DMG",
        ["UpdateRunInstaller"] = "Run Installer",
        ["UpdateShowAppImage"] = "Show AppImage",
        ["UpdateShowInFinder"] = "Show in Finder",
        ["UpdateShowInExplorer"] = "Show in Explorer",
        ["UpdateOk"] = "OK",
        ["UpdateClose"] = "Close",
        ["UpdateTryAgain"] = "Try Again",
        ["UpdateButtonUpdate"] = "Update",
        ["UpdateButtonUpdateName"] = "Update to {0}",
        ["UpdateButtonDownloading"] = "Downloading · {0}%",
        ["UpdateButtonDownloadingUnknown"] = "Downloading…",
        ["UpdateButtonInstall"] = "Install",
        ["UpdateButtonInstallName"] = "Install {0}",
        ["ErrorFileNotFoundTitle"] = "Couldn't find that file",
        ["ErrorAccessDeniedTitle"] = "Access denied",
        ["ErrorReadFailureTitle"] = "Couldn't read the file",
        ["ErrorUnsupportedTypeTitle"] = "This isn't Markdown",
        ["ErrorFileNotFoundDetails"] = "It may have been moved, renamed, or deleted.",
        ["ErrorAccessDeniedDetails"] = $"{AppProductInfo.Name} doesn't have permission to read this file.",
        ["ErrorUnsupportedTypeDetails"] = $"{AppProductInfo.Name} opens {{0}} and {{1}} files.",
        ["ErrorUnsupportedTypeSingleDetails"] = $"{AppProductInfo.Name} opens {{0}} files.",
        ["DirtyPromptTitle"] = "Save changes to \"{0}\"?",
        ["DirtyPromptCloseFile"] = "Otherwise your changes will be lost when the tab closes.",
        ["DirtyPromptCloseFolder"] = "Otherwise your changes will be lost when the folder closes.",
        ["DirtyPromptReload"] = "Otherwise your changes will be lost on reload.",
        ["DirtyPromptLeaveEditMode"] = "Otherwise your changes will be lost when you leave editing.",
        ["DirtyPromptCloseWindow"] = $"Otherwise your changes will be lost when you quit {AppProductInfo.Name}.",
        ["SaveInvalidPath"] = "Couldn't save to this path: {0}",
        ["SaveAccessDenied"] = "Access denied: {0}",
        ["SaveWriteFailure"] = "Couldn't save the document: {0}",
        ["SaveGenericFailure"] = "Couldn't save the document.",
        ["OpenDialogTitle"] = "Open Markdown file",
        ["OpenFolderDialogTitle"] = "Open folder",
        ["ExternalChangeTitle"] = "{0} changed on disk.",
        ["ExternalChangeMessage"] = "Another program saved its version, and your edits aren't saved yet.",
        ["ExternalChangeReloadTooltip"] = "Your edits will be lost",
        ["ExternalChangeReload"] = "Load from disk",
        ["ExternalChangeKeep"] = "Keep my edits",
        ["TabDeletedSuffix"] = "(deleted)",
        ["SidebarHideTooltip"] = "Hide file panel ({0})",
        ["SidebarShowTooltip"] = "Show file panel ({0})",
        ["SidebarNewFile"] = "New File",
        ["SidebarNewFolder"] = "New Folder",
        ["SidebarCreateTooltip"] = "New file or folder",
        ["SidebarOpenAnotherFolder"] = "Open Another Folder…",
        ["TreeRename"] = "Rename",
        ["TreeDuplicate"] = "Duplicate",
        ["TreeDelete"] = "Delete",
        ["TreeOpenInNewTab"] = "Open in New Tab",
        ["TreeRevealInExplorerWindows"] = "Show in Explorer",
        ["TreeRevealInExplorerMacOS"] = "Show in Finder",
        ["TreeRevealInExplorerLinux"] = "Show in File Manager",
        ["TreeNameTaken"] = "A file with this name already exists",
        ["TreeFolderNameTaken"] = "A folder with this name already exists",
        ["TreeInvalidChars"] = """These characters aren't allowed: \ / : * ? " < > |""",
        ["TreeReservedName"] = "This name is reserved by the system",
        ["TreeOperationFailed"] = "The operation failed",
        ["DeleteFileTitle"] = "Delete \"{0}\"?",
        ["DeleteFileBody"] = "The file will be moved to the recycle bin. If it is open in a tab, that tab will close.",
        ["DeleteFolderTitle"] = "Delete folder \"{0}\"?",
        ["DeleteFolderBody"] = "The folder will be moved to the recycle bin.",
        ["DeleteFolderNonEmptyTitle"] = "Delete folder \"{0}\" and everything in it?",
        ["DeleteFolderNonEmptyBody"] = "The folder has {1} items. Everything will be moved to the recycle bin. Open tabs from this folder will close.",
        ["DeletePermanentBody"] = "The recycle bin is not available here. The item will be deleted permanently and cannot be restored.",
        ["DeleteUnsavedChangesWarning"] = "Unsaved changes in \"{0}\" will be lost.",
        ["DeleteConfirm"] = "Delete",
        ["DeletePermanentConfirm"] = "Delete permanently",
        ["DeleteCancel"] = "Cancel",
        ["FileOpErrorTitle"] = "Couldn't delete \"{0}\"",
        ["FileOpErrorClose"] = "Close",
        ["SidebarSearchPlaceholder"] = "Search files",
        ["SidebarSearchReset"] = "Esc to clear search",
        ["SidebarSearchEmpty"] = "No matches in this folder",
        ["SidebarSearchMatches"] = "MATCHES",
        ["SidebarSearchTruncated"] = "Showing the first matches only. Narrow your query.",
        ["TabsOverflow"] = "{0} more",
        ["TabsOverflowHeader"] = "OPEN TABS",
        ["TabsCloseOthers"] = "Close Others",
        ["TabClose"] = "Close tab",
        ["NewDocumentTooltip"] = "New document ({0})",
        ["EmptySurfaceTitle"] = "No document selected",
        ["EmptySurfaceHint"] = "Pick a file on the left — it opens in a tab.",
        ["EmptyFolderTitle"] = "No Markdown files in this folder yet",
        ["EmptyFolderHint"] = "Create the first one — it will appear in the list on the left.",
        ["SidebarNoDocuments"] = "No .md files here",
        ["AppMenuCloseFolderLabel"] = "Close Folder",
        ["WelcomeOpenFolder"] = "Open Folder…",
        ["SidebarTooltip"] = "Files in this folder",
        ["TreeNodeMissing"] = "Folder is gone",
        ["TreeNodeAccessDenied"] = "Access denied",
        ["TreeNodeReadError"] = "Couldn't read this folder",
        ["FolderErrorNotFoundTitle"] = "Couldn't find that folder",
        ["FolderErrorAccessDeniedTitle"] = "Access denied",
        ["FolderErrorReadTitle"] = "Couldn't read that folder",
        ["SaveDialogTitle"] = "Save Markdown file",
        ["MarkdownDocuments"] = "Markdown documents",
        ["UntitledFileName"] = "Untitled.md"
    };

    private static readonly Dictionary<string, string> Russian = new(StringComparer.Ordinal)
    {
        ["WelcomeTagline"] = "Тихое место для чтения Markdown.",
        ["WelcomeNewDocument"] = "Новый документ",
        ["WelcomeOpenFile"] = "Открыть файл…",
        ["RecentTitle"] = "НЕДАВНИЕ",
        ["RecentClear"] = "Очистить",
        ["RecentToday"] = "сегодня",
        ["RecentYesterday"] = "вчера",
        ["RecentDateFormat"] = "d MMM",
        ["RecentDateWithYearFormat"] = "d MMM yyyy",
        ["RecentFileNotFoundTitle"] = "Файл не найден",
        ["RecentFolderNotFoundTitle"] = "Папка не найдена",
        ["RecentRemoveBody"] = "Убрать «{0}» из «Недавних»?",
        ["RecentRemoveConfirm"] = "Убрать",
        ["RecentRemoveCancel"] = "Отмена",
        ["WelcomeDropHint"] = "или перетащите сюда .md файл или папку",
        ["TitleBarMinimize"] = "Свернуть",
        ["TitleBarMaximize"] = "Развернуть",
        ["TitleBarRestore"] = "Восстановить",
        ["TitleBarClose"] = "Закрыть",
        ["AppMenuTooltip"] = "Меню",
        ["EditToggleTooltip"] = "Переключить режим редактирования ({0})",
        ["EditDone"] = "Готово",
        ["EditDoneTooltip"] = "Закончить правку ({0})",
        ["EditUnsaved"] = "Не сохранено",
        ["ReadingSettingsTooltip"] = "Вид: тема, шрифт, размер",
        ["OverlayCloseSettings"] = "Закрыть настройки",
        ["AppSettingsHeader"] = "Настройки",
        ["AppMenuNewDocument"] = "Новый документ",
        ["AppMenuOpenFile"] = "Открыть файл…",
        ["AppMenuOpenFolder"] = "Открыть папку…",
        ["AppMenuSave"] = "Сохранить",
        ["AppMenuSaveAs"] = "Сохранить как…",
        ["AppMenuReload"] = "Перечитать с диска",
        ["AppMenuFilesPanel"] = "Панель файлов",
        ["AppMenuCloseTab"] = "Закрыть вкладку",
        ["AppMenuSettings"] = "Настройки…",
        ["AppMenuCheckForUpdates"] = "Проверить обновления…",
        ["AppMenuAbout"] = $"О {AppProductInfo.Name}",
        ["AboutWindowTitle"] = $"О {AppProductInfo.Name}",
        ["AboutVersion"] = "Версия {0}",
        ["AboutForkAttribution"] = "Форк MarkMello © 2026 MarkMello contributors",
        ["AppSettingsReadingHintPrefix"] = "Тема, шрифт и размер текста — в карточке",
        ["AppSettingsReadingHintSuffix"] = "над документом.",
        ["LanguageLabel"] = "Язык",
        ["LanguageHint"] = "Оболочка и диалоги",
        ["LanguageSystem"] = "Системный",
        ["LanguageEnglish"] = "Английский",
        ["LanguageRussian"] = "Русский",
        ["ReadingThemeLabel"] = "Тема",
        ["ReadingThemeAuto"] = "Авто",
        ["ReadingThemeLight"] = "Светлая",
        ["ReadingThemeDark"] = "Тёмная",
        ["ReadingFontLabel"] = "Шрифт",
        ["ReadingFontSerif"] = "С засечками",
        ["ReadingFontSans"] = "Без засечек",
        ["ReadingFontMono"] = "Моно",
        ["ReadingSizeLabel"] = "Размер текста",
        ["ReadingFontSizeValue"] = "{0} px",
        ["ReadingSizeDecreaseTooltip"] = "Меньше ({0})",
        ["ReadingSizeIncreaseTooltip"] = "Больше ({0})",
        ["ReadingLineHeightLabel"] = "Интерлиньяж",
        ["ReadingWidthLabel"] = "Ширина строки",
        ["ReadingWidthNarrow"] = "Узкая",
        ["ReadingWidthMedium"] = "Средняя",
        ["ReadingWidthWide"] = "Широкая",
        ["WindowBorderLabel"] = "Рамка окна",
        ["WindowBorderHint"] = "Контур по краю окна",
        ["WindowBorderAuto"] = "Авто",
        ["WindowBorderOn"] = "Вкл",
        ["WindowBorderOff"] = "Выкл",
        ["DocumentOutlineLabel"] = "Оглавление",
        ["DocumentOutlineHint"] = "Рельс разделов у правого края документа",
        ["DocumentOutlineOn"] = "Вкл",
        ["DocumentOutlineOff"] = "Выкл",
        ["ReadingMoreSettingsHint"] = "Язык, обновления, версия",
        ["ReadingMoreSettingsLink"] = "Настройки…",
        ["StatusWordsOne"] = "{0:N0} слово",
        ["StatusWordsFew"] = "{0:N0} слова",
        ["StatusWordsMany"] = "{0:N0} слов",
        ["StatusReadMinutes"] = "{0} мин",
        ["DragDropHint"] = "Отпустите, чтобы открыть",
        ["DropFileDetails"] = "{0} откроется в новой вкладке",
        ["DropFolderNewWindowDetails"] = "Папка {0} откроется в новом окне",
        ["DropFolderOpenElsewhereDetails"] = "Папка {0} уже открыта в другом окне",
        ["DropFolderThisWindowDetails"] = "Папка {0} откроется в этом окне",
        ["DirtyPromptCancel"] = "Отмена",
        ["DirtyPromptDiscard"] = "Не сохранять",
        ["DirtyPromptSave"] = "Сохранить",
        ["LoadErrorOpenAnotherFile"] = "Открыть другой файл…",
        ["LoadErrorTryAgain"] = "Повторить",
        ["LoadErrorDismiss"] = "закрыть",
        ["EditorBoldTooltip"] = "Жирный",
        ["EditorItalicTooltip"] = "Курсив",
        ["EditorCodeTooltip"] = "Код",
        ["EditorLinkTooltip"] = "Ссылка",
        ["EditorListTooltip"] = "Список",
        ["EditorQuoteTooltip"] = "Цитата",
        ["EditorProtectedImageDataMessage"] = "Встроенные данные изображения можно редактировать только целой строкой.",
        ["ContextCopy"] = "Копировать",
        ["ContextSelectAll"] = "Выделить всё",
        ["ContextCopyLink"] = "Копировать ссылку",
        ["ContextCopyLinks"] = "Копировать ссылки",
        ["ContextCopyTelegramMarkdown"] = "Копировать выделение как Markdown для Telegram",
        ["CodeCopyTooltip"] = "Скопировать код",
        ["CodeCopiedStatus"] = "Код скопирован",
        ["AlertNote"] = "Примечание",
        ["AlertTip"] = "Совет",
        ["AlertImportant"] = "Важно",
        ["AlertWarning"] = "Предупреждение",
        ["AlertCaution"] = "Внимание",
        ["ImageLoading"] = "Загрузка…",
        ["DiagramRenderFailed"] = "Не удалось отрисовать диаграмму {0}",
        ["DiagramSvgUnsupported"] = "Диаграмма {0} отрисована, но её SVG встроенный просмотр пока не поддерживает",
        ["DiagramEmpty"] = "Диаграмма получилась пустой. Проверьте синтаксис.",
        ["EditorSourceLabel"] = "Markdown",
        ["EditorPreviewLabel"] = "Предпросмотр",
        ["FindPlaceholder"] = "Поиск в документе",
        ["FindPreviousTooltip"] = "Предыдущее совпадение ({0})",
        ["FindToggleTooltip"] = "Найти в документе ({0})",
        ["FindNextTooltip"] = "Следующее совпадение ({0})",
        ["FindCloseTooltip"] = "Закрыть поиск ({0})",
        ["FindResultCount"] = "{0} из {1}",
        ["FindNoResults"] = "Ничего не найдено",
        ["UpdateWindowTitle"] = $"Обновление {AppProductInfo.Name}",
        ["UpdateCheckingTitle"] = "Проверяем обновления…",
        ["UpdateCheckingMessage"] = $"Ищем новую версию {AppProductInfo.Name} на GitHub.",
        ["UpdateAvailableTitle"] = "Доступна версия {0}",
        ["UpdateAvailableMessageDmg"] = $"У вас {AppProductInfo.Name} {{0}}. Скачаем установщик для этого Mac — {{1}}.",
        ["UpdateAvailableMessageInstaller"] = $"У вас {AppProductInfo.Name} {{0}}. Скачаем установщик для этого компьютера — {{1}}.",
        ["UpdateAvailableMessageAppImage"] = $"У вас {AppProductInfo.Name} {{0}}. Скачаем новый AppImage — {{1}}.",
        ["UpdateWhatsNew"] = "Что нового в {0} ↗",
        ["UpdateDownloadingTitle"] = "Загрузка {0}",
        ["UpdateDownloadCaption"] = "{0} · {1} %",
        ["UpdateDownloadedTitle"] = "Версия {0} скачана",
        ["UpdateDownloadedMessageDmg"] = $"Откройте DMG и перетащите {AppProductInfo.Name} в «Программы». Перед этим закройте приложение.",
        ["UpdateDownloadedMessageInstaller"] = $"Запустите установщик — он обновит {AppProductInfo.Name}. Перед этим закройте приложение.",
        ["UpdateDownloadedMessageAppImage"] = "Замените прежний AppImage новым. Перед этим закройте приложение.",
        ["UpdateOpenFailedMessage"] = "Не удалось открыть {0}. Откройте его из папки.",
        ["UpdateUpToDateTitle"] = "У вас последняя версия",
        ["UpdateUpToDateMessage"] = $"{AppProductInfo.Name} {{0}} — самая новая из опубликованных.",
        ["UpdateUnavailableTitle"] = "Для этой сборки обновления недоступны",
        ["UpdateSourceNotConfiguredMessage"] = "В этой сборке не указан источник релизов.",
        ["UpdateUnsupportedPlatformMessage"] = "Для {0} {1} нет готовых сборок.",
        ["UpdateCheckFailedTitle"] = "Не удалось проверить обновления",
        ["UpdateCheckFailedMessage"] = "Нет связи с GitHub. Проверьте интернет и попробуйте ещё раз.",
        ["UpdateCheckFailedServiceMessage"] = "GitHub не вернул подходящий релиз. Попробуйте позже.",
        ["UpdateDownloadFailedTitle"] = "Не удалось скачать обновление",
        ["UpdateDownloadFailedMessage"] = "Загрузка прервалась. Проверьте интернет и попробуйте ещё раз.",
        ["UpdateDownloadFailedOtherMessage"] = "Не удалось скачать обновление. Попробуйте позже.",
        ["UpdateCancel"] = "Отмена",
        ["UpdateLater"] = "Позже",
        ["UpdateDownload"] = "Скачать",
        ["UpdateOpenDmg"] = "Открыть DMG",
        ["UpdateRunInstaller"] = "Запустить установщик",
        ["UpdateShowAppImage"] = "Показать AppImage",
        ["UpdateShowInFinder"] = "Показать в Finder",
        ["UpdateShowInExplorer"] = "Показать в Проводнике",
        ["UpdateOk"] = "OK",
        ["UpdateClose"] = "Закрыть",
        ["UpdateTryAgain"] = "Повторить",
        ["UpdateButtonUpdate"] = "Обновить",
        ["UpdateButtonUpdateName"] = "Обновить до {0}",
        ["UpdateButtonDownloading"] = "Загрузка · {0} %",
        ["UpdateButtonDownloadingUnknown"] = "Загрузка…",
        ["UpdateButtonInstall"] = "Установить",
        ["UpdateButtonInstallName"] = "Установить {0}",
        ["ErrorFileNotFoundTitle"] = "Не удалось найти файл",
        ["ErrorAccessDeniedTitle"] = "Доступ запрещён",
        ["ErrorReadFailureTitle"] = "Не удалось прочитать файл",
        ["ErrorUnsupportedTypeTitle"] = "Это не Markdown",
        ["ErrorFileNotFoundDetails"] = "Возможно, его переместили, переименовали или удалили.",
        ["ErrorAccessDeniedDetails"] = $"У {AppProductInfo.Name} нет прав на чтение этого файла.",
        ["ErrorUnsupportedTypeDetails"] = $"{AppProductInfo.Name} открывает файлы {{0}} и {{1}}.",
        ["ErrorUnsupportedTypeSingleDetails"] = $"{AppProductInfo.Name} открывает файлы {{0}}.",
        ["DirtyPromptTitle"] = "Сохранить изменения в «{0}»?",
        ["DirtyPromptCloseFile"] = "Иначе правки пропадут, когда вкладка закроется.",
        ["DirtyPromptCloseFolder"] = "Иначе правки пропадут, когда папка закроется.",
        ["DirtyPromptReload"] = "Иначе правки пропадут при перезагрузке.",
        ["DirtyPromptLeaveEditMode"] = "Иначе правки пропадут при выходе из правки.",
        ["DirtyPromptCloseWindow"] = $"Иначе правки пропадут при выходе из {AppProductInfo.Name}.",
        ["SaveInvalidPath"] = "Не удалось сохранить по этому пути: {0}",
        ["SaveAccessDenied"] = "Доступ запрещён: {0}",
        ["SaveWriteFailure"] = "Не удалось сохранить документ: {0}",
        ["SaveGenericFailure"] = "Не удалось сохранить документ.",
        ["OpenDialogTitle"] = "Открыть Markdown-файл",
        ["OpenFolderDialogTitle"] = "Открыть папку",
        ["ExternalChangeTitle"] = "{0} изменён на диске.",
        ["ExternalChangeMessage"] = "Другая программа сохранила свою версию, а ваши правки ещё не сохранены.",
        ["ExternalChangeReloadTooltip"] = "Ваши правки пропадут",
        ["ExternalChangeReload"] = "Загрузить с диска",
        ["ExternalChangeKeep"] = "Оставить мои правки",
        ["TabDeletedSuffix"] = "(удалён)",
        ["SidebarHideTooltip"] = "Скрыть панель файлов ({0})",
        ["SidebarShowTooltip"] = "Показать панель файлов ({0})",
        ["SidebarNewFile"] = "Новый файл",
        ["SidebarNewFolder"] = "Новая папка",
        ["SidebarCreateTooltip"] = "Новый файл или папка",
        ["SidebarOpenAnotherFolder"] = "Открыть другую папку…",
        ["TreeRename"] = "Переименовать",
        ["TreeDuplicate"] = "Дублировать",
        ["TreeDelete"] = "Удалить",
        ["TreeOpenInNewTab"] = "Открыть в новой вкладке",
        ["TreeRevealInExplorerWindows"] = "Показать в проводнике",
        ["TreeRevealInExplorerMacOS"] = "Показать в Finder",
        ["TreeRevealInExplorerLinux"] = "Показать в файловом менеджере",
        ["TreeNameTaken"] = "Файл с таким именем уже есть",
        ["TreeFolderNameTaken"] = "Папка с таким именем уже есть",
        ["TreeInvalidChars"] = """Нельзя использовать: \ / : * ? " < > |""",
        ["TreeReservedName"] = "Это имя занято системой",
        ["TreeOperationFailed"] = "Операция не удалась",
        ["DeleteFileTitle"] = "Удалить «{0}»?",
        ["DeleteFileBody"] = "Файл будет перемещён в корзину. Если он открыт во вкладке, вкладка закроется.",
        ["DeleteFolderTitle"] = "Удалить папку «{0}»?",
        ["DeleteFolderBody"] = "Папка будет перемещена в корзину.",
        ["DeleteFolderNonEmptyTitle"] = "Удалить папку «{0}» и всё её содержимое?",
        ["DeleteFolderNonEmptyBody"] = "В папке {1} элементов. Всё будет перемещено в корзину. Открытые вкладки из этой папки закроются.",
        ["DeletePermanentBody"] = "Корзина здесь недоступна. Элемент будет удалён безвозвратно, восстановить его будет нельзя.",
        ["DeleteUnsavedChangesWarning"] = "Несохранённые правки в «{0}» пропадут.",
        ["DeleteConfirm"] = "Удалить",
        ["DeletePermanentConfirm"] = "Удалить навсегда",
        ["DeleteCancel"] = "Отмена",
        ["FileOpErrorTitle"] = "Не удалось удалить «{0}»",
        ["FileOpErrorClose"] = "Закрыть",
        ["SidebarSearchPlaceholder"] = "Поиск по файлам",
        ["SidebarSearchReset"] = "Esc — сбросить поиск",
        ["SidebarSearchEmpty"] = "Ничего не найдено в этой папке",
        ["SidebarSearchMatches"] = "СОВПАДЕНИЯ",
        ["SidebarSearchTruncated"] = "Показаны только первые совпадения. Уточните запрос.",
        ["TabsOverflow"] = "ещё {0}",
        ["TabsOverflowHeader"] = "ОТКРЫТЫЕ ВКЛАДКИ",
        ["TabsCloseOthers"] = "Закрыть все, кроме активной",
        ["TabClose"] = "Закрыть вкладку",
        ["NewDocumentTooltip"] = "Новый документ ({0})",
        ["EmptySurfaceTitle"] = "Документ не выбран",
        ["EmptySurfaceHint"] = "Выберите файл в списке слева — он откроется во вкладке.",
        ["EmptyFolderTitle"] = "В папке пока нет Markdown-файлов",
        ["EmptyFolderHint"] = "Создайте первый — он появится в списке слева.",
        ["SidebarNoDocuments"] = "Здесь нет файлов .md",
        ["AppMenuCloseFolderLabel"] = "Закрыть папку",
        ["WelcomeOpenFolder"] = "Открыть папку…",
        ["SidebarTooltip"] = "Файлы этой папки",
        ["TreeNodeMissing"] = "Папка исчезла",
        ["TreeNodeAccessDenied"] = "Доступ запрещён",
        ["TreeNodeReadError"] = "Не удалось прочитать папку",
        ["FolderErrorNotFoundTitle"] = "Не удалось найти папку",
        ["FolderErrorAccessDeniedTitle"] = "Доступ запрещён",
        ["FolderErrorReadTitle"] = "Не удалось прочитать папку",
        ["SaveDialogTitle"] = "Сохранить Markdown-файл",
        ["MarkdownDocuments"] = "Markdown-документы",
        ["UntitledFileName"] = "Безымянный.md"
    };

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

    private AppLanguage _selectedLanguage;
    private AppLanguage _effectiveLanguage;
    private CultureInfo _culture = EnglishCulture;

    public LocalizationService()
        : this(AppLanguage.System)
    {
    }

    public LocalizationService(AppLanguage initialLanguage)
    {
        SetLanguage(initialLanguage);
    }

    public AppLanguage SelectedLanguage => _selectedLanguage;

    public AppLanguage EffectiveLanguage => _effectiveLanguage;

    public CultureInfo Culture => _culture;

    public string this[string key] => ResolveString(key);

    public string Format(string key, params object?[] args)
        => string.Format(_culture, ResolveString(key), args);

    public string FormatPlural(string key, int count)
        => string.Format(_culture, ResolveString(key + PluralForm(count)), count);

    public void SetLanguage(AppLanguage language)
    {
        var normalized = NormalizeLanguage(language);
        var effective = ResolveEffectiveLanguage(normalized);
        var culture = ResolveCulture(effective);

        var selectedChanged = _selectedLanguage != normalized;
        var effectiveChanged = _effectiveLanguage != effective;
        var cultureChanged = !_culture.Equals(culture);
        if (!selectedChanged && !effectiveChanged && !cultureChanged)
        {
            return;
        }

        _selectedLanguage = normalized;
        _effectiveLanguage = effective;
        _culture = culture;

        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(EffectiveLanguage));
        OnPropertyChanged(nameof(Culture));
        NotifyLocalizedTextChanged();
    }

    private void NotifyLocalizedTextChanged()
    {
        // Avalonia indexer bindings may subscribe to either the CLR indexer
        // property name (Item) or the common WPF-style indexer marker (Item[]).
        // Raising both keeps every active shell/view binding refreshed when the
        // language changes. The empty name is the standard full-refresh signal.
        OnPropertyChanged("Item");
        OnPropertyChanged("Item[]");
        OnPropertyChanged(string.Empty);
    }

    private string ResolveString(string key)
    {
        var primary = _effectiveLanguage == AppLanguage.Russian ? Russian : English;
        if (primary.TryGetValue(key, out var value))
        {
            return value;
        }

        if (English.TryGetValue(key, out value))
        {
            return value;
        }

        return $"[[{key}]]";
    }

    /// <summary>
    /// Форма числительного для ключа <c>&lt;key&gt;One|Few|Many</c>. В английском форм
    /// две — единственное и остальное; в русском три, по последней цифре числа,
    /// кроме одиннадцати-четырнадцати.
    /// </summary>
    private string PluralForm(int count)
    {
        if (_effectiveLanguage != AppLanguage.Russian)
        {
            return count == 1 ? "One" : "Many";
        }

        var withinHundred = Math.Abs(count) % 100;
        var lastDigit = withinHundred % 10;

        if (lastDigit == 1 && withinHundred != 11)
        {
            return "One";
        }

        return lastDigit is >= 2 and <= 4 && withinHundred is < 12 or > 14 ? "Few" : "Many";
    }

    private static AppLanguage NormalizeLanguage(AppLanguage language)
        => language switch
        {
            AppLanguage.English => AppLanguage.English,
            AppLanguage.Russian => AppLanguage.Russian,
            _ => AppLanguage.System
        };

    private static AppLanguage ResolveEffectiveLanguage(AppLanguage selectedLanguage)
    {
        if (selectedLanguage is AppLanguage.English or AppLanguage.Russian)
        {
            return selectedLanguage;
        }

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Russian
            : AppLanguage.English;
    }

    private static CultureInfo ResolveCulture(AppLanguage language)
        => language == AppLanguage.Russian ? RussianCulture : EnglishCulture;
}
