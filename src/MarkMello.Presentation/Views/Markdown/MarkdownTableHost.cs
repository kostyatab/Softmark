using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Горизонтальная прокрутка таблицы, которая выходит на поля страницы, как в Notion.
/// </summary>
/// <remarks>
/// Сам хост занимает колонку чтения, а его <see cref="ScrollViewer"/> — всю ширину
/// страницы (прокручиваемой области документа) за вычетом
/// <see cref="PageEdgeInset"/> с каждой стороны. Слева таблицу отодвигает отступ
/// на ширину левого поля, поэтому в покое она начинается от колонки чтения,
/// широкая уходит в правое поле, а при прокрутке — в левое. Таблица уже колонки
/// остаётся в колонке шириной в свои колонки.
/// <para>
/// Где колонка стоит на странице, известно только после раскладки, поэтому поля
/// пересчитываются по <see cref="Layoutable.LayoutUpdated"/>: изменились — ещё
/// один проход раскладки таблицы. Вне прокручиваемой области (тесты, превью без
/// страницы) полей нет, и таблица прокручивается внутри колонки.
/// </para>
/// <para>
/// У края, за которым таблица продолжается, она плавно затухает
/// (<see cref="EdgeFadeWidth"/>): маска прозрачности на области прокрутки, не на
/// полосе прокрутки. Ближе к концу прокрутки затухание сужается вместе со скрытым
/// остатком, поэтому не пропадает скачком.
/// </para>
/// </remarks>
internal sealed class MarkdownTableHost : Decorator
{
    /// <summary>Отступ таблицы от краёв страницы.</summary>
    public const double PageEdgeInset = 32;

    /// <summary>Ширина затухания у края, за которым таблица продолжается.</summary>
    public const double EdgeFadeWidth = 40;

    /// <summary>
    /// Сколько места справа на странице занято оверлеем у её правого края: таблица
    /// до него не доходит. Наследуется, ставится на контейнер документа.
    /// </summary>
    public static readonly AttachedProperty<double> PageEndReserveProperty =
        AvaloniaProperty.RegisterAttached<MarkdownTableHost, Control, double>("PageEndReserve", inherits: true);

    private const double BleedTolerance = 0.5;

    private readonly double _scrollBarReserve;
    private ScrollViewer? _page;
    private double _bleedLeft;
    private double _bleedRight;
    private double _fadeViewportWidth;

    public MarkdownTableHost(MarkdownTablePanel panel, double scrollBarReserve)
    {
        Panel = panel;
        Panel.HorizontalAlignment = HorizontalAlignment.Left;
        _scrollBarReserve = scrollBarReserve;

        // Cell text does not wrap: a table wider than its room scrolls
        // horizontally as a whole, like a code block.
        ScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Classes = { ScrollBarReveal.PersistentHorizontalClass },
            Content = Panel
        };
        ScrollViewer.ScrollChanged += (_, _) =>
        {
            UpdatePanelMargin();
            UpdateEdgeFade();
        };
        Child = ScrollViewer;

        LayoutUpdated += (_, _) => UpdateBleed();
    }

    public MarkdownTablePanel Panel { get; }

    public ScrollViewer ScrollViewer { get; }

    /// <summary>Ширина затухания у левого края области прокрутки, 0 — без него.</summary>
    internal double LeftFade { get; private set; }

    /// <summary>Ширина затухания у правого края области прокрутки, 0 — без него.</summary>
    internal double RightFade { get; private set; }

    public static double GetPageEndReserve(Control control) => control.GetValue(PageEndReserveProperty);

    public static void SetPageEndReserve(Control control, double value) => control.SetValue(PageEndReserveProperty, value);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _page = this.FindAncestorOfType<ScrollViewer>();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _page = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PageEndReserveProperty)
        {
            UpdateBleed();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columnWidth = availableSize.Width;
        Panel.ReadingColumnWidth = double.IsFinite(columnWidth) ? columnWidth : double.NaN;

        ScrollViewer.Measure(availableSize.Inflate(new Thickness(_bleedLeft, 0, _bleedRight, 0)));
        var desired = ScrollViewer.DesiredSize;
        return new Size(
            double.IsFinite(columnWidth) ? columnWidth : Math.Max(0, desired.Width - _bleedLeft - _bleedRight),
            desired.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ScrollViewer.Arrange(new Rect(
            -_bleedLeft,
            0,
            finalSize.Width + _bleedLeft + _bleedRight,
            finalSize.Height));
        return finalSize;
    }

    private void UpdateBleed()
    {
        var left = 0d;
        var right = 0d;
        if (_page is { Viewport.Width: > 0 } page && Bounds.Width > 0
            && this.TranslatePoint(default, page) is { } origin)
        {
            left = Math.Max(0, Math.Floor(origin.X - PageEdgeInset));
            right = Math.Max(0, Math.Floor(
                page.Viewport.Width - PageEdgeInset - GetPageEndReserve(this) - (origin.X + Bounds.Width)));
        }

        if (Math.Abs(left - _bleedLeft) < BleedTolerance && Math.Abs(right - _bleedRight) < BleedTolerance)
        {
            return;
        }

        _bleedLeft = left;
        _bleedRight = right;
        UpdatePanelMargin();
        InvalidateMeasure();
    }

    /// <summary>
    /// Слева — поле страницы, чтобы таблица в покое стояла в колонке чтения. Снизу —
    /// место под полосу прокрутки, только пока есть что прокручивать: таблица,
    /// которая помещается, сохраняет свою высоту.
    /// </summary>
    private void UpdatePanelMargin()
        => Panel.Margin = new Thickness(
            _bleedLeft,
            0,
            0,
            ScrollViewer.Extent.Width > ScrollViewer.Viewport.Width + BleedTolerance ? _scrollBarReserve : 0);

    private void UpdateEdgeFade()
    {
        var viewportWidth = ScrollViewer.Viewport.Width;
        var left = 0d;
        var right = 0d;
        if (viewportWidth > EdgeFadeWidth * 2)
        {
            // Края таблицы в координатах области прокрутки: слева её отодвигает
            // поле страницы, справа она кончается вместе с содержимым.
            var offset = ScrollViewer.Offset.X;
            left = FadeFor(offset - _bleedLeft);
            right = FadeFor(ScrollViewer.Extent.Width - offset - viewportWidth);
        }

        // The mask stops are fractions of the viewport, so a new viewport width
        // needs a new mask even when the fades themselves stay the same.
        if (Math.Abs(left - LeftFade) < BleedTolerance
            && Math.Abs(right - RightFade) < BleedTolerance
            && Math.Abs(viewportWidth - _fadeViewportWidth) < BleedTolerance
            && (left > 0 || right > 0) == (ScrollViewer.Presenter?.OpacityMask is not null))
        {
            return;
        }

        if (ScrollViewer.Presenter is not { } presenter)
        {
            return;
        }

        LeftFade = left;
        RightFade = right;
        _fadeViewportWidth = viewportWidth;
        presenter.OpacityMask = left <= 0 && right <= 0
            ? null
            : CreateEdgeFadeMask(left / viewportWidth, right / viewportWidth);
    }

    /// <summary>Затухание по скрытому за краем остатку таблицы.</summary>
    private static double FadeFor(double hiddenWidth)
        => hiddenWidth < BleedTolerance ? 0 : Math.Min(hiddenWidth, EdgeFadeWidth);

    /// <summary>
    /// Маске важна только прозрачность: непрозрачный цвет оставляет таблицу видимой,
    /// прозрачный — прячет.
    /// </summary>
    private static LinearGradientBrush CreateEdgeFadeMask(double leftFraction, double rightFraction)
        => new()
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(leftFraction > 0 ? Colors.Transparent : Colors.Black, 0),
                new GradientStop(Colors.Black, leftFraction),
                new GradientStop(Colors.Black, 1 - rightFraction),
                new GradientStop(rightFraction > 0 ? Colors.Transparent : Colors.Black, 1)
            }
        };
}
