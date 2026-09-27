using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MarkMello.Domain;
using MarkMello.Domain.Outline;
using MarkMello.Presentation.ViewModels;
using MarkMello.Presentation.Views.Markdown;
using MarkMello.Presentation.Views.Markdown.Outline;
using System.ComponentModel;

namespace MarkMello.Presentation.Views;

public partial class ViewerView : UserControl, IFindHost
{
    private const double KeyboardPageOverlap = 48.0;
    private ScrollViewer? _scroll;
    private MarkdownDocumentView? _documentView;
    private bool _hasRenderedDocument;

    // Идёт пересборка после докраски кода (ADR-0010 §4), а не новый документ.
    private bool _isRecolorRender;

    /// <summary>Рельс не подходит к тексту ближе этого — иначе он скрыт.</summary>
    private const double OutlineMinimumGapToText = 16;

    private DocumentOutlineLayer? _outlineLayer;
    private ShellViewModel? _viewModel;
    private RenderedMarkdownDocument? _outlineDocument;
    private DocumentOutline _outline = DocumentOutline.Empty;
    private double[] _outlineHeadingTops = [];
    private bool _isOutlineBuildQueued;

    // Документ перерисовывается: рельс остаётся на месте до новой сборки, чтобы не
    // мигать и не дёргать широкие таблицы, но по его устаревшим пунктам не ходим.
    private bool _isOutlineStale;

    public ViewerView()
    {
        InitializeComponent();
    }

    // ---------- IFindHost ----------

    public string? ActiveQuery => _documentView?.ActiveSearchQuery;

    public int MatchIndex => _documentView?.MatchIndex ?? -1;

    public int MatchCount => _documentView?.MatchCount ?? 0;

    public event EventHandler? FindStateChanged;

    public void ApplyQuery(string? query) => _documentView?.ApplySearchQuery(query);

    public void FindNext() => _documentView?.FindNext();

    public void FindPrevious() => _documentView?.FindPrevious();

    public void ClearFind() => _documentView?.ApplySearchQuery(null);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        AttachViewModel(DataContext as ShellViewModel);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroll = this.FindControl<ScrollViewer>("DocScroll");
        if (_scroll is not null)
        {
            _scroll.ScrollChanged += OnScrollChanged;
        }

        AddHandler(KeyDownEvent, OnViewerKeyDown, RoutingStrategies.Tunnel);

        _documentView = this.FindControl<MarkdownDocumentView>("DocumentView");
        if (_documentView is not null)
        {
            _documentView.DocumentRendered += OnDocumentRendered;
            _documentView.DocumentRenderInvalidated += OnDocumentRenderInvalidated;
            _documentView.MarkdownFileLinkRequested += OnMarkdownFileLinkRequested;
            _documentView.SearchStateChanged += OnDocumentSearchStateChanged;
            _documentView.SizeChanged += OnOutlineGeometryChanged;
        }

        _outlineLayer = this.FindControl<DocumentOutlineLayer>("OutlineLayer");
        if (_outlineLayer is not null)
        {
            _outlineLayer.IsCardSuppressed = IsOutlineCardSuppressed;
            _outlineLayer.EntryInvoked += OnOutlineEntryInvoked;

            // Рельс и карточка лежат рядом с DocScroll, а не в нём: колесо над ними,
            // которое не прокрутило список карточки, должно крутить документ.
            _outlineLayer.AddHandler(InputElement.PointerWheelChangedEvent, OnOutlineWheelChanged);
        }

        SizeChanged += OnOutlineGeometryChanged;
        AttachViewModel(DataContext as ShellViewModel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _hasRenderedDocument = false;
        _isRecolorRender = false;

        SizeChanged -= OnOutlineGeometryChanged;
        AttachViewModel(null);
        HideOutline();
        if (_outlineLayer is not null)
        {
            _outlineLayer.IsCardSuppressed = null;
            _outlineLayer.EntryInvoked -= OnOutlineEntryInvoked;
            _outlineLayer.RemoveHandler(InputElement.PointerWheelChangedEvent, OnOutlineWheelChanged);
            _outlineLayer = null;
        }

        if (_scroll is not null)
        {
            _scroll.ScrollChanged -= OnScrollChanged;
            _scroll = null;
        }

        RemoveHandler(KeyDownEvent, OnViewerKeyDown);

        if (_documentView is not null)
        {
            _documentView.DocumentRendered -= OnDocumentRendered;
            _documentView.DocumentRenderInvalidated -= OnDocumentRenderInvalidated;
            _documentView.MarkdownFileLinkRequested -= OnMarkdownFileLinkRequested;
            _documentView.SearchStateChanged -= OnDocumentSearchStateChanged;
            _documentView.SizeChanged -= OnOutlineGeometryChanged;
            _documentView = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>
    /// Колесо над рельсом или карточкой оглавления, которое не прокрутило список карточки,
    /// передаётся области прокрутки документа: она крутит его штатно, тем же шагом, что и
    /// над самим документом, — так же Avalonia передаёт колесо с полосы прокрутки.
    /// </summary>
    private void OnOutlineWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || _scroll?.Presenter is not { } presenter || TopLevel.GetTopLevel(this) is not { } root)
        {
            return;
        }

        var forwarded = new PointerWheelEventArgs(
            presenter,
            e.Pointer,
            root,
            e.GetPosition(root),
            e.Timestamp,
            e.GetCurrentPoint(root).Properties,
            e.KeyModifiers,
            e.Delta);
        presenter.RaiseEvent(forwarded);
        e.Handled = forwarded.Handled;
    }

    private void OnViewerKeyDown(object? sender, KeyEventArgs e)
    {
        if (_scroll is null || e.Handled || DataContext is not ShellViewModel { IsViewer: true, IsEditMode: false })
        {
            return;
        }

        if (HasCommandModifier(e.KeyModifiers) || e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            return;
        }

        var nextOffsetY = GetKeyboardScrollOffset(
            e.Key,
            e.KeyModifiers,
            _scroll.Offset.Y,
            _scroll.ScrollBarMaximum.Y,
            _scroll.SmallChange.Height,
            _scroll.Viewport.Height);

        if (nextOffsetY is null || Math.Abs(nextOffsetY.Value - _scroll.Offset.Y) <= double.Epsilon)
        {
            return;
        }

        _scroll.Offset = new Vector(_scroll.Offset.X, nextOffsetY.Value);
        e.Handled = true;
    }

    internal static double? GetKeyboardScrollOffset(
        Key key,
        KeyModifiers modifiers,
        double currentOffset,
        double maximumOffset,
        double smallChange,
        double viewportHeight)
    {
        var max = Math.Max(0, maximumOffset);
        var current = Math.Clamp(currentOffset, 0, max);
        var lineStep = smallChange > 0 ? smallChange : 40.0;
        var pageStep = Math.Max(lineStep, viewportHeight - KeyboardPageOverlap);

        var target = key switch
        {
            Key.Down => current + lineStep,
            Key.Up => current - lineStep,
            Key.PageDown => current + pageStep,
            Key.PageUp => current - pageStep,
            Key.Home => 0,
            Key.End => max,
            Key.Space when modifiers.HasFlag(KeyModifiers.Shift) => current - pageStep,
            Key.Space => current + pageStep,
            _ => (double?)null,
        };

        return target is null ? null : Math.Clamp(target.Value, 0, max);
    }

    private static bool HasCommandModifier(KeyModifiers modifiers)
        => modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);

    private void OnDocumentRendered(object? sender, EventArgs e)
    {
        if (_isRecolorRender)
        {
            // Докраска кода: тот же документ, другие только цвета. Фокус
            // и прокрутка к совпадению поиска остаются как были.
            _isRecolorRender = false;
            QueueOutlineBuild();
            return;
        }

        if (DataContext is ShellViewModel vm)
        {
            vm.MarkReadableDocumentRendered();
            vm.StartPendingCodeHighlighting();
            RestorePendingScrollOffset(vm);
        }

        _hasRenderedDocument = true;
        FocusDocumentViewAsync();
        QueueOutlineBuild();

        // Keep the active search match in view after a document re-render.
        if (_documentView?.MatchIndex >= 0)
        {
            _documentView.ScrollToActiveMatch();
        }
    }

    /// <summary>
    /// Возврат на вкладку восстанавливает её позицию прокрутки. Делается после отрисовки:
    /// до неё ScrollBarMaximum ещё нулевой и любое смещение схлопнется в ноль.
    /// </summary>
    private void RestorePendingScrollOffset(ShellViewModel viewModel)
    {
        if (viewModel.TakePendingScrollOffset() is not { } offset || _scroll is null)
        {
            return;
        }

        if (offset <= 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (_scroll is null)
                {
                    return;
                }

                var target = Math.Clamp(offset, 0, _scroll.ScrollBarMaximum.Y);
                _scroll.Offset = new Vector(_scroll.Offset.X, target);
            },
            DispatcherPriority.Background);
    }

    private void OnDocumentSearchStateChanged(object? sender, EventArgs e)
        => FindStateChanged?.Invoke(this, EventArgs.Empty);

    private void FocusDocumentViewAsync()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_documentView is not null && DataContext is ShellViewModel { IsViewer: true, IsEditMode: false })
            {
                _documentView.Focus(NavigationMethod.Unspecified);
            }
        }, DispatcherPriority.Background);
    }

    private void OnDocumentRenderInvalidated(object? sender, EventArgs e)
    {
        _isRecolorRender = _hasRenderedDocument
            && DataContext is ShellViewModel vm
            && vm.ConsumeRecolor(_documentView?.Document);
        if (_isRecolorRender)
        {
            return;
        }

        _hasRenderedDocument = false;

        // Пустой документ не присылает DocumentRendered — рельс прячется сразу.
        if (_documentView?.Document is not { Blocks.Count: > 0 })
        {
            HideOutline();
            return;
        }

        _isOutlineStale = true;
        _outlineLayer?.CloseCard();
    }

    private async void OnMarkdownFileLinkRequested(object? sender, MarkdownFileLinkRequestedEventArgs e)
    {
        if (DataContext is not ShellViewModel vm)
        {
            return;
        }

        await vm.OpenLinkedDocumentAsync(e.TargetPath).ConfigureAwait(true);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_scroll is null)
        {
            return;
        }

        var max = _scroll.ScrollBarMaximum.Y;
        var current = _scroll.Offset.Y;
        if (DataContext is ShellViewModel vm)
        {
            vm.ReadingProgress = max > 0 ? Math.Clamp(current / max * 100.0, 0, 100) : 0;

            // Позиция уезжает во вкладку на каждое изменение: при переключении
            // вьюер уже показывает другой документ и спрашивать его поздно.
            vm.ReportScrollOffset(current);
        }

        UpdateOutlineCurrentEntry();
    }

    // ---------- Оглавление ----------

    private void AttachViewModel(ShellViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ShellViewModel.IsDocumentOutlineEnabled):
                QueueOutlineBuild();
                break;
            case nameof(ShellViewModel.DocumentOutlineLabel) when _outlineLayer is not null:
                _outlineLayer.AccessibleName = _viewModel?.DocumentOutlineLabel;
                break;
        }

        // Открылся другой оверлей — карточка оглавления уступает ему место.
        if (_outlineLayer is { IsCardOpen: true } && IsOutlineCardSuppressed())
        {
            _outlineLayer.CloseCard();
        }
    }

    private bool IsOutlineCardSuppressed()
        => DataContext is not ShellViewModel vm
            || vm.HasOpenOverlay
            || vm.IsFindBarOpen
            || vm.IsModalDialogOpen
            || _isOutlineStale;

    private void OnOutlineGeometryChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_hasRenderedDocument)
        {
            QueueOutlineBuild();
        }
    }

    /// <summary>
    /// Оглавление строится после отрисовки документа и не раньше: с фоновым
    /// приоритетом, одной сборкой на серию событий (ресайз, смена ширины строки).
    /// Документ, перерисованный между постановкой и сборкой, ловит проверка
    /// <see cref="_hasRenderedDocument"/>: до нового DocumentRendered она ложна,
    /// и сборку повторит сам DocumentRendered.
    /// </summary>
    private void QueueOutlineBuild()
    {
        if (_isOutlineBuildQueued)
        {
            return;
        }

        _isOutlineBuildQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _isOutlineBuildQueued = false;
                BuildOutline();
            },
            DispatcherPriority.Background);
    }

    private void BuildOutline()
    {
        // Документ ещё перерисовывается — соберём по его DocumentRendered.
        if (!_hasRenderedDocument)
        {
            return;
        }

        if (_outlineLayer is null
            || _documentView?.Document is not { } document
            || _scroll is null
            || DataContext is not ShellViewModel { IsDocumentOutlineEnabled: true } vm)
        {
            HideOutline();
            return;
        }

        if (!ReferenceEquals(_outlineDocument, document))
        {
            _outlineDocument = document;
            _outline = DocumentOutline.Create(document);
        }

        if (!_outline.HasEnoughEntries || !TryMeasureHeadingTops(_outline, out var tops) || !OutlineFitsBesideText())
        {
            HideOutline();
            return;
        }

        _outlineHeadingTops = tops;
        _outlineLayer.AccessibleName = vm.DocumentOutlineLabel;
        _isOutlineStale = false;
        _outlineLayer.Show(_outline.Entries, FindCurrentOutlineEntry());
        MarkdownTableHost.SetPageEndReserve(_scroll, DocumentOutlineLayer.RailFootprint);
    }

    private bool TryMeasureHeadingTops(DocumentOutline outline, out double[] tops)
    {
        tops = new double[outline.Entries.Count];
        for (var index = 0; index < tops.Length; index++)
        {
            var heading = _documentView?.GetTopLevelHeadingControl(outline.Entries[index].BlockIndex);
            if (heading?.TranslatePoint(default, _scroll!) is not { } point)
            {
                return false;
            }

            tops[index] = _scroll!.Offset.Y + point.Y;
        }

        return true;
    }

    /// <summary>Рельс стоит в правом поле и не наезжает на текст — иначе он скрыт.</summary>
    private bool OutlineFitsBesideText()
    {
        if (_documentView is null)
        {
            return false;
        }

        var textRight = _documentView.TranslatePoint(
            new Point(_documentView.Bounds.Width - _documentView.DocumentPadding.Right, 0),
            this);
        if (textRight is null)
        {
            return false;
        }

        var railLeft = Bounds.Width - DocumentOutlineLayer.RailFootprint;
        return railLeft - textRight.Value.X >= OutlineMinimumGapToText;
    }

    private int FindCurrentOutlineEntry()
        => _scroll is null
            ? 0
            : Math.Max(0, DocumentOutline.FindCurrentEntry(
                _outlineHeadingTops,
                _scroll.Offset.Y,
                _scroll.ScrollBarMaximum.Y,
                _scroll.Viewport.Height));

    private void UpdateOutlineCurrentEntry()
    {
        if (_outlineLayer is { IsVisible: true } && !_isOutlineStale && _outlineHeadingTops.Length > 0)
        {
            _outlineLayer.SetCurrentIndex(FindCurrentOutlineEntry());
        }
    }

    private void HideOutline()
    {
        _isOutlineStale = false;
        _outlineHeadingTops = [];
        _outlineLayer?.Hide();
        if (_scroll is not null)
        {
            MarkdownTableHost.SetPageEndReserve(_scroll, 0);
        }
    }

    private void OnOutlineEntryInvoked(object? sender, int index)
    {
        if (_isOutlineStale || index < 0 || index >= _outline.Entries.Count)
        {
            return;
        }

        _documentView?.TryScrollToTopLevelHeading(_outline.Entries[index].BlockIndex);
    }
}
