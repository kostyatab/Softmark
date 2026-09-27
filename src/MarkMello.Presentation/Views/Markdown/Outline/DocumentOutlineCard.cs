using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using MarkMello.Domain.Outline;

namespace MarkMello.Presentation.Views.Markdown.Outline;

/// <summary>
/// Карточка оглавления: все разделы документа с отступом по уровню, текущий —
/// акцентом. Длинный пункт в одну строку с тающим краем, полный текст — в
/// подсказке, как у вкладок. Создаётся по первому наведению на рельс.
/// </summary>
internal sealed class DocumentOutlineCard : Border
{
    public const double ItemHeight = 28;
    public const double LevelIndent = 14;
    private const double ItemHorizontalPadding = 10;

    private readonly ScrollViewer _scroll;
    private readonly StackPanel _items;
    private int _currentIndex = -1;
    private bool _isScrollToCurrentPending;

    public DocumentOutlineCard()
    {
        Classes.Add("mm-outline-card");
        _items = new StackPanel();
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _items
        };
        Child = _scroll;
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>
    /// Селектор типа в стилях сопоставляется точно: без этого <c>Border.mm-outline-card</c>
    /// не находит наследника, и карточка остаётся без фона, рамки и тени.
    /// </summary>
    protected override Type StyleKeyOverride => typeof(Border);

    /// <summary>Нажатие на пункт: его индекс в оглавлении.</summary>
    public event EventHandler<int>? EntryInvoked;

    internal IReadOnlyList<Button> Items => _items.Children.OfType<Button>().ToList();

    public void SetEntries(IReadOnlyList<DocumentOutlineEntry> entries, int currentIndex)
    {
        _items.Children.Clear();
        for (var index = 0; index < entries.Count; index++)
        {
            _items.Children.Add(CreateItem(entries[index], index));
        }

        _currentIndex = -1;
        SetCurrentIndex(currentIndex);
    }

    public void SetCurrentIndex(int currentIndex)
    {
        if (_currentIndex == currentIndex)
        {
            return;
        }

        if (GetItem(_currentIndex) is { } previous)
        {
            previous.Classes.Remove("mm-current");
        }

        _currentIndex = currentIndex;
        GetItem(currentIndex)?.Classes.Add("mm-current");
    }

    /// <summary>При открытии текущий пункт сразу виден — он встаёт в середину списка.</summary>
    public void ScrollCurrentIntoViewOnNextLayout()
    {
        _isScrollToCurrentPending = true;
        InvalidateArrange();
    }

    private Button CreateItem(DocumentOutlineEntry entry, int index)
    {
        var label = new TrailingFadeDecorator
        {
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = entry.Text }
        };
        var item = new Button
        {
            Height = ItemHeight,
            // Клавиатурной навигации у оглавления нет; клик не забирает фокус у документа.
            Focusable = false,
            Padding = new Thickness(
                ItemHorizontalPadding + (entry.Level - 1) * LevelIndent,
                0,
                ItemHorizontalPadding,
                0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = label,
            Tag = index
        };
        item.Classes.Add("mm-list-item");
        item.Classes.Add("mm-outline-item");
        ToolTip.SetTip(item, entry.Text);

        // Подсказка — полный текст обрезанного пункта. Целиком видный пункт её не
        // показывает: при прокрутке списка подсказки иначе открывались бы одна за другой.
        ToolTip.AddToolTipOpeningHandler(item, (_, e) => e.Cancel = label.Fade <= 0);
        AutomationProperties.SetName(item, entry.Text);
        item.Click += OnItemClick;
        return item;
    }

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int index })
        {
            e.Handled = true;
            EntryInvoked?.Invoke(this, index);
        }
    }

    private Button? GetItem(int index)
        => index >= 0 && index < _items.Children.Count ? _items.Children[index] as Button : null;

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_isScrollToCurrentPending || _scroll.Viewport.Height <= 0)
        {
            return;
        }

        _isScrollToCurrentPending = false;
        if (GetItem(_currentIndex) is not { } current)
        {
            return;
        }

        var target = current.Bounds.Y - (_scroll.Viewport.Height - current.Bounds.Height) / 2;
        var maximum = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        _scroll.Offset = new Vector(0, Math.Clamp(target, 0, maximum));
    }
}
