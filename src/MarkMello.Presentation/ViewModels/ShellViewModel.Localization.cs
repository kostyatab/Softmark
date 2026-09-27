using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkMello.Application.UseCases;
using MarkMello.Domain;
using MarkMello.Presentation.Localization;
using System.ComponentModel;

namespace MarkMello.Presentation.ViewModels;

public partial class ShellViewModel
{
    private PendingDirtyActionKind? _dirtyPromptKind;
    private SaveDocumentResult? _dirtyPromptErrorResult;
    private OpenDocumentResult? _loadErrorResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSystemLanguageSelected))]
    [NotifyPropertyChangedFor(nameof(IsEnglishLanguageSelected))]
    [NotifyPropertyChangedFor(nameof(IsRussianLanguageSelected))]
    private AppLanguage _language = AppLanguage.System;

    public bool IsSystemLanguageSelected => Language == AppLanguage.System;

    public bool IsEnglishLanguageSelected => Language == AppLanguage.English;

    public bool IsRussianLanguageSelected => Language == AppLanguage.Russian;

    private IReadOnlyList<LanguageSelectionItem>? _languageOptions;

    // Список один на всё время жизни: ComboBox пишет выбор обратно в модель, и если
    // подменить ItemsSource посреди выбора, он находит в новом списке прежний пункт и
    // откатывает выбор. При смене языка меняются только подписи пунктов.
    public IReadOnlyList<LanguageSelectionItem> LanguageOptions =>
        _languageOptions ??= CreateLanguageOptions();

    public LanguageSelectionItem? SelectedLanguageOption
    {
        get => LanguageOptions.FirstOrDefault(option => option.Language == Language);
        set
        {
            if (value is not null)
            {
                ApplyLanguageSelection(value.Language);
            }
        }
    }

    private static readonly string[] LocalizedBindingPropertyNames =
    [
        nameof(AppMenuCheckForUpdates),
        nameof(AppMenuAbout),
        nameof(AppMenuCloseFolderLabel),
        nameof(AppMenuCloseTab),
        nameof(AppMenuFilesPanel),
        nameof(AppMenuNewDocument),
        nameof(AppMenuOpenFile),
        nameof(AppMenuOpenFolder),
        nameof(AppMenuReload),
        nameof(AppMenuSave),
        nameof(AppMenuSaveAs),
        nameof(AppMenuSettings),
        nameof(AppMenuTooltip),
        nameof(AppSettingsHeader),
        nameof(AppSettingsReadingHintPrefix),
        nameof(AppSettingsReadingHintSuffix),
        nameof(DirtyPromptCancel),
        nameof(DocumentOutlineHint),
        nameof(DocumentOutlineLabel),
        nameof(DocumentOutlineOff),
        nameof(DocumentOutlineOn),
        nameof(DirtyPromptDiscard),
        nameof(DirtyPromptSave),
        nameof(DragDropHint),
        nameof(EditDoneLabel),
        nameof(EditDoneTooltip),
        nameof(EditToggleTooltip),
        nameof(EditUnsavedLabel),
        nameof(FindCloseTooltip),
        nameof(FindNextTooltip),
        nameof(FindPlaceholder),
        nameof(FindPreviousTooltip),
        nameof(FindToggleTooltip),
        nameof(LanguageHint),
        nameof(LanguageLabel),
        nameof(LoadErrorOpenAnotherFile),
        nameof(LoadErrorDismiss),
        nameof(LoadErrorTryAgain),
        nameof(NewDocumentTooltip),
        nameof(OverlayCloseSettings),
        nameof(ReadingFontLabel),
        nameof(ReadingFontMono),
        nameof(ReadingFontSans),
        nameof(ReadingFontSerif),
        nameof(ReadingLineHeightLabel),
        nameof(WindowBorderAuto),
        nameof(WindowBorderHint),
        nameof(WindowBorderLabel),
        nameof(WindowBorderOff),
        nameof(WindowBorderOn),
        nameof(ReadingMoreSettingsHint),
        nameof(ReadingMoreSettingsLink),
        nameof(ReadingSettingsTooltip),
        nameof(ReadingSizeDecreaseTooltip),
        nameof(ReadingSizeIncreaseTooltip),
        nameof(ReadingSizeLabel),
        nameof(ReadingThemeAuto),
        nameof(ReadingThemeDark),
        nameof(ReadingThemeLabel),
        nameof(ReadingThemeLight),
        nameof(ReadingWidthLabel),
        nameof(ReadingWidthMedium),
        nameof(ReadingWidthNarrow),
        nameof(ReadingWidthWide),
        nameof(TitleBarClose),
        nameof(TitleBarMaximize),
        nameof(TitleBarMinimize),
        nameof(TitleBarRestore),
        nameof(WelcomeNewDocument),
        nameof(RecentTitle),
        nameof(RecentClear),
        nameof(RecentRemoveConfirm),
        nameof(RecentRemoveCancel),
        nameof(EmptySurfaceHint),
        nameof(EmptySurfaceTitle),
        nameof(EmptyFolderHint),
        nameof(EmptyFolderTitle),
        nameof(SidebarNoDocuments),
        nameof(ExternalChangeKeep),
        nameof(ExternalChangeMessage),
        nameof(ExternalChangeReload),
        nameof(ExternalChangeReloadTooltip),
        nameof(ExternalChangeTitle),
        nameof(SidebarCreateTooltip),
        nameof(SidebarNewFile),
        nameof(SidebarNewFolder),
        nameof(SidebarOpenAnotherFolder),
        nameof(SidebarSearchEmpty),
        nameof(SidebarSearchMatches),
        nameof(SidebarSearchPlaceholder),
        nameof(SidebarSearchReset),
        nameof(SidebarSearchTruncated),
        nameof(SidebarToggleTooltip),
        nameof(SidebarTooltip),
        nameof(TabClose),
        nameof(TreeDelete),
        nameof(TreeDuplicate),
        nameof(TreeOpenInNewTab),
        nameof(TreeRename),
        nameof(TreeRevealInExplorer),
        nameof(TabsCloseOthers),
        nameof(TabsOverflowHeader),
        nameof(TabsOverflowLabel),
        nameof(WelcomeDropHint),
        nameof(WelcomeOpenFile),
        nameof(WelcomeOpenFolder),
        nameof(WelcomeTagline),
    ];

    public string AppMenuCloseFolderLabel => _localization["AppMenuCloseFolderLabel"];

    public string SidebarNewFile => _localization["SidebarNewFile"];
    public string SidebarNewFolder => _localization["SidebarNewFolder"];
    public string SidebarCreateTooltip => _localization["SidebarCreateTooltip"];
    public string SidebarOpenAnotherFolder => _localization["SidebarOpenAnotherFolder"];

    /// <summary>
    /// Одна кнопка на оба направления: в шапке сайдбара она скрывает панель, в строке
    /// окна при свёрнутой панели — показывает.
    /// </summary>
    public string SidebarToggleTooltip => _localization.Format(
        IsSidebarCollapsed ? "SidebarShowTooltip" : "SidebarHideTooltip",
        ToggleSidebarShortcut);
    public string TreeDelete => _localization["TreeDelete"];
    public string TreeDuplicate => _localization["TreeDuplicate"];
    public string TreeOpenInNewTab => _localization["TreeOpenInNewTab"];
    public string TreeRename => _localization["TreeRename"];

    /// <summary>Название файлового менеджера зависит от платформы, поэтому ключей три.</summary>
    public string TreeRevealInExplorer => _localization[_platform.PlatformName switch
    {
        "macOS" => "TreeRevealInExplorerMacOS",
        "Linux" => "TreeRevealInExplorerLinux",
        _ => "TreeRevealInExplorerWindows"
    }];

    public string SidebarSearchEmpty => _localization["SidebarSearchEmpty"];
    public string SidebarSearchMatches => _localization["SidebarSearchMatches"];
    public string SidebarSearchPlaceholder => _localization["SidebarSearchPlaceholder"];
    public string SidebarSearchReset => _localization["SidebarSearchReset"];
    public string SidebarSearchTruncated => _localization["SidebarSearchTruncated"];
    public string SidebarTooltip => _localization["SidebarTooltip"];
    public string TabClose => _localization["TabClose"];
    public string TabsCloseOthers => _localization["TabsCloseOthers"];
    public string TabsOverflowHeader => _localization["TabsOverflowHeader"];
    public string EmptySurfaceHint => _localization["EmptySurfaceHint"];
    public string EmptySurfaceTitle => _localization["EmptySurfaceTitle"];
    public string EmptyFolderHint => _localization["EmptyFolderHint"];
    public string EmptyFolderTitle => _localization["EmptyFolderTitle"];
    public string SidebarNoDocuments => _localization["SidebarNoDocuments"];

    /// <summary>«ещё N» — счётчик приходит из состава вкладок, поэтому свойство пересчитывается.</summary>
    public string TabsOverflowLabel => _localization.Format("TabsOverflow", OpenDocuments.OverflowTabs.Count);
    public string AppMenuNewDocument => _localization["AppMenuNewDocument"];
    public string AppMenuOpenFile => _localization["AppMenuOpenFile"];
    public string AppMenuOpenFolder => _localization["AppMenuOpenFolder"];
    public string AppMenuSave => _localization["AppMenuSave"];
    public string AppMenuSaveAs => _localization["AppMenuSaveAs"];
    public string AppMenuReload => _localization["AppMenuReload"];
    public string AppMenuFilesPanel => _localization["AppMenuFilesPanel"];
    public string AppMenuCloseTab => _localization["AppMenuCloseTab"];
    public string AppMenuSettings => _localization["AppMenuSettings"];
    public string AppMenuCheckForUpdates => _localization["AppMenuCheckForUpdates"];

    public string AppMenuAbout => _localization["AppMenuAbout"];

    /// <summary>
    /// «О MarkMello» в меню ⋯ — только вне macOS (ADR-0009 Rule 4): там этот пункт
    /// живёт в системном меню приложения, и дублировать его — против привычки платформы.
    /// </summary>
    public bool ShowsAboutMenuItem => _showsAboutMenuItem;
    public string AppMenuTooltip => _localization["AppMenuTooltip"];
    public string AppSettingsHeader => _localization["AppSettingsHeader"];

    /// <summary>
    /// Подсказка вверху окна «Настройки»: между частями стоит значок Aa — та самая кнопка
    /// над документом, поэтому фраза разрезана на две.
    /// </summary>
    public string AppSettingsReadingHintPrefix => _localization["AppSettingsReadingHintPrefix"];
    public string AppSettingsReadingHintSuffix => _localization["AppSettingsReadingHintSuffix"];
    public string DirtyPromptCancel => _localization["DirtyPromptCancel"];
    public string DirtyPromptDiscard => _localization["DirtyPromptDiscard"];
    public string DirtyPromptSave => _localization["DirtyPromptSave"];

    /// <summary>
    /// Порядок кнопок диалога правок — как у платформы (ADR-0009 Rule 10). Кнопки встают
    /// в колонки ряда: 0 — у левого края, 1 — распорка, 2–4 — у правого по порядку.
    /// На macOS «Не сохранять» стоит слева особняком, справа «Отмена» и крайняя
    /// «Сохранить»; на Windows и Linux все справа, основная первой: «Сохранить»,
    /// «Не сохранять», «Отмена».
    /// </summary>
    public int DirtyPromptDiscardColumn => UsesMacOSDialogOrder ? 0 : 3;

    public int DirtyPromptCancelColumn => UsesMacOSDialogOrder ? 3 : 4;

    public int DirtyPromptSaveColumn => UsesMacOSDialogOrder ? 4 : 2;

    private bool UsesMacOSDialogOrder => string.Equals(_platform.PlatformName, "macOS", StringComparison.Ordinal);
    public string DragDropHint => _localization["DragDropHint"];
    public string EditToggleTooltip => _localization.Format("EditToggleTooltip", CommandShortcut(Key.E));

    /// <summary>«Готово» — та же команда, что карандаш, поэтому и сочетание то же.</summary>
    public string EditDoneLabel => _localization["EditDone"];
    public string EditDoneTooltip => _localization.Format("EditDoneTooltip", CommandShortcut(Key.E));
    public string EditUnsavedLabel => _localization["EditUnsaved"];
    public string FindCloseTooltip => _localization.Format("FindCloseTooltip", KeyShortcut(Key.Escape));
    public string FindNextTooltip => _localization.Format("FindNextTooltip", KeyShortcut(Key.Enter));
    public string FindPlaceholder => _localization["FindPlaceholder"];
    public string FindPreviousTooltip => _localization.Format("FindPreviousTooltip", KeyShortcut(Key.Enter, KeyModifiers.Shift));
    public string FindToggleTooltip => _localization.Format("FindToggleTooltip", CommandShortcut(Key.F));

    /// <summary>«+» после вкладок — то же, что сочетание, поэтому оно и стоит в подсказке.</summary>
    public string NewDocumentTooltip => _localization.Format("NewDocumentTooltip", CommandShortcut(Key.N));
    public string LanguageHint => _localization["LanguageHint"];
    public string LanguageLabel => _localization["LanguageLabel"];
    public string LoadErrorOpenAnotherFile => _localization["LoadErrorOpenAnotherFile"];
    public string LoadErrorDismiss => _localization["LoadErrorDismiss"];
    public string LoadErrorTryAgain => _localization["LoadErrorTryAgain"];
    public string OverlayCloseSettings => _localization["OverlayCloseSettings"];
    public string ReadingFontLabel => _localization["ReadingFontLabel"];
    public string ReadingFontMono => _localization["ReadingFontMono"];
    public string ReadingFontSans => _localization["ReadingFontSans"];
    public string ReadingFontSerif => _localization["ReadingFontSerif"];
    public string ReadingLineHeightLabel => _localization["ReadingLineHeightLabel"];
    public string WindowBorderAuto => _localization["WindowBorderAuto"];
    public string WindowBorderHint => _localization["WindowBorderHint"];
    public string WindowBorderLabel => _localization["WindowBorderLabel"];
    public string WindowBorderOff => _localization["WindowBorderOff"];
    public string WindowBorderOn => _localization["WindowBorderOn"];
    public string DocumentOutlineHint => _localization["DocumentOutlineHint"];
    public string DocumentOutlineLabel => _localization["DocumentOutlineLabel"];
    public string DocumentOutlineOff => _localization["DocumentOutlineOff"];
    public string DocumentOutlineOn => _localization["DocumentOutlineOn"];
    public string ReadingMoreSettingsHint => _localization["ReadingMoreSettingsHint"];
    public string ReadingMoreSettingsLink => _localization["ReadingMoreSettingsLink"];
    public string ReadingSettingsTooltip => _localization["ReadingSettingsTooltip"];
    public string ReadingSizeDecreaseTooltip => _localization.Format("ReadingSizeDecreaseTooltip", CommandShortcut(Key.OemMinus));
    public string ReadingSizeIncreaseTooltip => _localization.Format("ReadingSizeIncreaseTooltip", CommandShortcut(Key.OemPlus));
    public string ReadingSizeLabel => _localization["ReadingSizeLabel"];
    public string ReadingThemeAuto => _localization["ReadingThemeAuto"];
    public string ReadingThemeDark => _localization["ReadingThemeDark"];
    public string ReadingThemeLabel => _localization["ReadingThemeLabel"];
    public string ReadingThemeLight => _localization["ReadingThemeLight"];
    public string ReadingWidthLabel => _localization["ReadingWidthLabel"];
    public string ReadingWidthMedium => _localization["ReadingWidthMedium"];
    public string ReadingWidthNarrow => _localization["ReadingWidthNarrow"];
    public string ReadingWidthWide => _localization["ReadingWidthWide"];
    public string TitleBarClose => _localization["TitleBarClose"];
    public string TitleBarMaximize => _localization["TitleBarMaximize"];
    public string TitleBarMinimize => _localization["TitleBarMinimize"];
    public string TitleBarRestore => _localization["TitleBarRestore"];

    public string WelcomeNewDocument => _localization["WelcomeNewDocument"];
    public string RecentTitle => _localization["RecentTitle"];
    public string RecentClear => _localization["RecentClear"];
    public string RecentRemoveConfirm => _localization["RecentRemoveConfirm"];
    public string RecentRemoveCancel => _localization["RecentRemoveCancel"];
    public string WelcomeDropHint => _localization["WelcomeDropHint"];
    public string WelcomeOpenFile => _localization["WelcomeOpenFile"];
    public string WelcomeOpenFolder => _localization["WelcomeOpenFolder"];
    public string WelcomeTagline => _localization["WelcomeTagline"];

    /// <summary>
    /// Подписи сочетаний для меню и стартового экрана: ⌘ на macOS, Ctrl на Windows и Linux.
    /// От языка не зависят, поэтому в <see cref="LocalizedBindingPropertyNames"/> их нет.
    /// </summary>
    public string OpenFileShortcut => CommandShortcut(Key.O);

    public string OpenFolderShortcut => CommandShortcut(Key.O, KeyModifiers.Shift);

    public string ToggleSidebarShortcut => CommandShortcut(Key.B);

    public string NewDocumentShortcut => CommandShortcut(Key.N);

    /// <summary>Сочетание сохранения — плашка рядом с «Не сохранено» в строке окна.</summary>
    public string SaveShortcut => CommandShortcut(Key.S);

    public string SaveAsShortcut => CommandShortcut(Key.S, KeyModifiers.Shift);

    public string ReloadShortcut => CommandShortcut(Key.R);

    public string CloseTabShortcut => CommandShortcut(Key.W);

    /// <summary>Сочетание настроек приложения — плашка в нижней строке карточки Aa.</summary>
    public string SettingsShortcut => CommandShortcut(Key.OemComma);

    /// <summary>Клавиши строки дерева — подписи в её контекстном меню.</summary>
    public string TreeRenameShortcut => KeyShortcut(Key.F2);

    public string TreeDeleteShortcut => KeyShortcut(Key.Delete);

    /// <summary>
    /// Клавиши диалогов — в тултипах их кнопок: Enter подтверждает, Esc отменяет,
    /// ⌘⌫ (Ctrl+Backspace) — «Не сохранять».
    /// </summary>
    public string DialogConfirmShortcut => KeyShortcut(Key.Enter);

    public string DialogCancelShortcut => KeyShortcut(Key.Escape);

    public string DirtyPromptDiscardShortcut => CommandShortcut(Key.Back);

    /// <summary>Клавиши «Открыть файл» по отдельности — на стартовом экране каждая в своей плашке.</summary>
    public IReadOnlyList<string> OpenFileShortcutKeys
        => ShortcutLabel.Keys(ShortcutLabel.Command(Key.O, _platform.PlatformName), _platform.PlatformName);

    private string CommandShortcut(Key key, KeyModifiers extra = KeyModifiers.None)
        => ShortcutLabel.Format(ShortcutLabel.Command(key, _platform.PlatformName, extra), _platform.PlatformName);

    /// <summary>Сочетание без командной клавиши — например, ↵ и ⇧↵ в поле поиска.</summary>
    private string KeyShortcut(Key key, KeyModifiers modifiers = KeyModifiers.None)
        => ShortcutLabel.Format(new KeyGesture(key, modifiers), _platform.PlatformName);

    /// <summary>Плашка под документом: «499 слов · 3 мин».</summary>
    public string ReadingStatusLabel
        => _localization.FormatPlural("StatusWords", WordCount)
           + " · "
           + _localization.Format("StatusReadMinutes", ReadTimeMinutes);

    [RelayCommand]
    private void SelectSystemLanguage() => ApplyLanguageSelection(AppLanguage.System);

    [RelayCommand]
    private void SelectEnglishLanguage() => ApplyLanguageSelection(AppLanguage.English);

    [RelayCommand]
    private void SelectRussianLanguage() => ApplyLanguageSelection(AppLanguage.Russian);

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsLocalizationChangeNotification(e.PropertyName))
        {
            return;
        }

        RefreshLocalizedProperties();

        // Даты строк «Недавних» («сегодня», «15 сент.») зависят от языка.
        if (_isRecentListShown)
        {
            _ = RebuildRecentRowsAsync();
        }
    }

    partial void OnLanguageChanged(AppLanguage value)
    {
        OnPropertyChanged(nameof(SelectedLanguageOption));
    }

    private static bool IsLocalizationChangeNotification(string? propertyName)
        => string.IsNullOrEmpty(propertyName)
           || propertyName == nameof(ILocalizationService.SelectedLanguage)
           || propertyName == nameof(ILocalizationService.EffectiveLanguage)
           || propertyName == nameof(ILocalizationService.Culture)
           || propertyName == "Item"
           || propertyName == "Item[]";

    private void ApplyLanguageSelection(AppLanguage language, bool persist = true)
    {
        var normalized = NormalizeLanguage(language);
        if (Language == normalized && _localization.SelectedLanguage == normalized)
        {
            return;
        }

        Language = normalized;
        _localization.SetLanguage(normalized);
        UpdateDraftFileName();

        if (persist)
        {
            PersistLanguage(normalized);
        }
    }

    private void PersistLanguage(AppLanguage language)
    {
        try
        {
            _settings.SaveLanguageAsync(language).AsTask().GetAwaiter().GetResult();
        }
        catch
        {
            // Language persistence remains best-effort for the same reason
            // as the rest of the lightweight app settings.
        }
    }

    private void RefreshLocalizedProperties()
    {
        RefreshLanguageOptionLabels();

        NotifyLocalizedBindingPropertiesChanged();
        EditorSession?.RefreshLocalizedProperties();

        OnPropertyChanged(nameof(FindResultLabel));
        OnPropertyChanged(nameof(IsSystemLanguageSelected));
        OnPropertyChanged(nameof(IsEnglishLanguageSelected));
        OnPropertyChanged(nameof(IsRussianLanguageSelected));
        OnPropertyChanged(nameof(ReadingStatusLabel));
        OnPropertyChanged(nameof(FontSizeLabel));
        OnPropertyChanged(nameof(LineHeightLabel));

        RefreshDirtyPromptTexts();
        RefreshLoadErrorTexts();
    }

    private void NotifyLocalizedBindingPropertiesChanged()
    {
        foreach (var propertyName in LocalizedBindingPropertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private IReadOnlyList<LanguageSelectionItem> CreateLanguageOptions() =>
    [
        new(AppLanguage.System, _localization[LanguageLabelKey(AppLanguage.System)]),
        new(AppLanguage.English, _localization[LanguageLabelKey(AppLanguage.English)]),
        new(AppLanguage.Russian, _localization[LanguageLabelKey(AppLanguage.Russian)])
    ];

    private void RefreshLanguageOptionLabels()
    {
        if (_languageOptions is null)
        {
            return;
        }

        foreach (var option in _languageOptions)
        {
            option.Label = _localization[LanguageLabelKey(option.Language)];
        }
    }

    private static string LanguageLabelKey(AppLanguage language)
        => language switch
        {
            AppLanguage.English => "LanguageEnglish",
            AppLanguage.Russian => "LanguageRussian",
            _ => "LanguageSystem"
        };

    private void SetDirtyPrompt(PendingDirtyActionKind kind)
    {
        _dirtyPromptKind = kind;
        _dirtyPromptErrorResult = null;
        RefreshDirtyPromptTexts();

        // «ещё N» — единственное меню, пункт которого (✕) меню не закрывает, а меню «+»
        // остаётся открытым под ⌘W из его карточки: вопрос о правках встал бы вторым
        // оверлеем поверх открытой карточки.
        if (IsTabsOverflowMenuOpen || IsNewTabMenuOpen)
        {
            ShellOverlay = ShellOverlayKind.None;
        }

        IsDirtyPromptOpen = true;
    }

    private void SetDirtyPromptError(SaveDocumentResult? result)
    {
        _dirtyPromptErrorResult = result;
        RefreshDirtyPromptTexts();
    }

    /// <summary>
    /// Заголовок называет файл — грязных вкладок может быть несколько, — а текст говорит,
    /// когда пропадут правки, если их не сохранить.
    /// </summary>
    private void RefreshDirtyPromptTexts()
    {
        DirtyPromptTitle = _dirtyPromptKind is null
            ? string.Empty
            : Format("DirtyPromptTitle", _dirtyPromptTab?.Title ?? FileName);

        DirtyPromptMessage = _dirtyPromptKind switch
        {
            PendingDirtyActionKind.CloseFile => _localization["DirtyPromptCloseFile"],
            PendingDirtyActionKind.CloseFolder => _localization["DirtyPromptCloseFolder"],
            PendingDirtyActionKind.Reload => _localization["DirtyPromptReload"],
            PendingDirtyActionKind.LeaveEditMode => _localization["DirtyPromptLeaveEditMode"],
            PendingDirtyActionKind.CloseWindow => _localization["DirtyPromptCloseWindow"],
            _ => string.Empty
        };

        DirtyPromptErrorMessage = GetSaveFailureMessage(_dirtyPromptErrorResult);
    }

    private void ClearDirtyPromptState()
    {
        _pendingDirtyAction = null;
        _dirtyPromptTab = null;
        _dirtyPromptKind = null;
        _dirtyPromptErrorResult = null;
        IsDirtyPromptOpen = false;
        DirtyPromptTitle = string.Empty;
        DirtyPromptMessage = string.Empty;
        DirtyPromptErrorMessage = string.Empty;
    }

    private void SetLoadError(OpenDocumentResult result)
    {
        _loadErrorResult = result;
        RefreshLoadErrorTexts();
        State = ViewState.LoadError;
    }

    /// <summary>
    /// Экран ошибки по A-LoadError: заголовок говорит, что случилось, пояснение — почему
    /// и что делать, путь отдельной строкой, чтобы его можно было скопировать.
    /// </summary>
    private void RefreshLoadErrorTexts()
    {
        if (_loadErrorResult is null)
        {
            return;
        }

        ErrorKind = _loadErrorResult switch
        {
            OpenDocumentResult.NotFound => LoadErrorKind.NotFound,
            OpenDocumentResult.AccessDenied => LoadErrorKind.AccessDenied,
            OpenDocumentResult.UnsupportedType => LoadErrorKind.UnsupportedType,
            _ => LoadErrorKind.ReadFailure
        };

        ErrorTitle = _loadErrorResult switch
        {
            OpenDocumentResult.NotFound => _localization["ErrorFileNotFoundTitle"],
            OpenDocumentResult.AccessDenied => _localization["ErrorAccessDeniedTitle"],
            OpenDocumentResult.ReadError => _localization["ErrorReadFailureTitle"],
            OpenDocumentResult.UnsupportedType => _localization["ErrorUnsupportedTypeTitle"],
            _ => string.Empty
        };

        ErrorDescription = _loadErrorResult switch
        {
            OpenDocumentResult.NotFound => _localization["ErrorFileNotFoundDetails"],
            OpenDocumentResult.AccessDenied => _localization["ErrorAccessDeniedDetails"],
            OpenDocumentResult.ReadError read => read.Message,
            OpenDocumentResult.UnsupportedType => FormatSupportedExtensions(),
            _ => string.Empty
        };

        ErrorPath = FormatErrorPath(GetFailedPath(_loadErrorResult));
    }

    /// <summary>
    /// Путь на экране ошибки копируют в терминал или файловый менеджер. На macOS и Linux
    /// <c>~</c> там понимают, и путь сокращается, как в тултипе вкладки; в Проводнике и cmd
    /// <c>~\Documents</c> не откроется — там путь полный.
    /// </summary>
    private string FormatErrorPath(string? path)
        => string.Equals(_platform.PlatformName, "Windows", StringComparison.Ordinal)
            ? path ?? string.Empty
            : BuildTabTooltip(path);

    /// <summary>«.md, .markdown и .txt» — последний союзом, по правилам языка.</summary>
    private string FormatSupportedExtensions()
    {
        var extensions = SupportedDocumentTypes.Extensions;
        return extensions.Count == 1
            ? _localization.Format("ErrorUnsupportedTypeSingleDetails", extensions[0])
            : _localization.Format(
                "ErrorUnsupportedTypeDetails",
                string.Join(", ", extensions.Take(extensions.Count - 1)),
                extensions[^1]);
    }

    /// <summary>Ошибка папки — тот же экран, но без пояснения и без «Повторить».</summary>
    private void SetFolderLoadError(string titleKey, string path)
    {
        _loadErrorResult = null;
        ErrorKind = LoadErrorKind.Folder;
        ErrorTitle = _localization[titleKey];
        ErrorDescription = string.Empty;
        ErrorPath = FormatErrorPath(path);
        State = ViewState.LoadError;
    }

    private void ClearLoadError()
    {
        _loadErrorResult = null;
        ErrorKind = LoadErrorKind.None;
        ErrorTitle = string.Empty;
        ErrorDescription = string.Empty;
        ErrorPath = string.Empty;
    }

    private void UpdateDraftFileName()
    {
        if (EditorSession is null || !string.IsNullOrWhiteSpace(EditorSession.CurrentPath))
        {
            return;
        }

        EditorSession.UpdateDraftFileName(GetUntitledFileName());
        RefreshDocumentSummary();
        RefreshWindowTitle();
    }

    private string GetUntitledFileName() => _localization["UntitledFileName"];

    private string GetSaveFailureMessage(SaveDocumentResult? result)
        => result switch
        {
            SaveDocumentResult.InvalidPath invalidPath => _localization.Format("SaveInvalidPath", invalidPath.Path),
            SaveDocumentResult.AccessDenied accessDenied => _localization.Format("SaveAccessDenied", accessDenied.Path),
            SaveDocumentResult.WriteError writeError => _localization.Format("SaveWriteFailure", writeError.Message),
            null => string.Empty,
            _ => _localization["SaveGenericFailure"]
        };

    private string NormalizeSuggestedFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return GetUntitledFileName();
        }

        return SupportedDocumentTypes.IsSupportedPath(fileName)
            ? fileName
            : $"{fileName}.md";
    }

    private static AppLanguage NormalizeLanguage(AppLanguage language)
        => language switch
        {
            AppLanguage.English => AppLanguage.English,
            AppLanguage.Russian => AppLanguage.Russian,
            _ => AppLanguage.System
        };

}

/// <summary>
/// Пункт выбора языка. Равенство — по ссылке: ComboBox должен узнавать свой пункт, а не
/// равный ему по значению из другого списка.
/// </summary>
public sealed class LanguageSelectionItem(AppLanguage language, string label) : ObservableObject
{
    private string _label = label;

    public AppLanguage Language { get; } = language;

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public override string ToString() => Label;
}

