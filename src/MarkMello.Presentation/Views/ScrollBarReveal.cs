using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace MarkMello.Presentation.Views;

/// <summary>
/// Полоса прокрутки видна, пока содержимое прокручивается, и уходит после паузы.
/// </summary>
/// <remarks>
/// Своё автоскрытие у <see cref="ScrollBar"/> реагирует только на курсор
/// (<see cref="ScrollBar.IsExpanded"/>), а на саму прокрутку — нет: при прокрутке
/// трекпадом индикатора позиции не было бы вовсе. Поэтому на время прокрутки
/// полоса получает класс <see cref="ScrollingClass"/>, а через
/// <see cref="ScrollBar.HideDelay"/> после последнего сдвига теряет его. Включает
/// поведение тема полосы (<c>Themes/ScrollBar.axaml</c>), так что оно работает во
/// всех областях прокрутки одинаково. Таймер заводится при первой прокрутке.
/// </remarks>
internal sealed class ScrollBarReveal
{
    /// <summary>Класс полосы, пока содержимое прокручивается.</summary>
    public const string ScrollingClass = "mm-scrolling";

    /// <summary>
    /// Класс области прокрутки, у которой горизонтальная полоса видна и в покое, пока
    /// содержимое не помещается: блоки документа, которые листаются вбок.
    /// </summary>
    public const string PersistentHorizontalClass = "mm-scrollbar-persistent";

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollBarReveal, ScrollBar, bool>("IsEnabled");

    private static readonly ConditionalWeakTable<ScrollBar, DispatcherTimer> HideTimers = new();

    static ScrollBarReveal()
    {
        RangeBase.ValueProperty.Changed.AddClassHandler<ScrollBar>(OnValueChanged);
        IsEnabledProperty.Changed.AddClassHandler<ScrollBar>(OnIsEnabledChanged);
    }

    private ScrollBarReveal()
    {
    }

    public static bool GetIsEnabled(ScrollBar scrollBar) => scrollBar.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ScrollBar scrollBar, bool value) => scrollBar.SetValue(IsEnabledProperty, value);

    private static void OnValueChanged(ScrollBar scrollBar, AvaloniaPropertyChangedEventArgs e)
    {
        if (!GetIsEnabled(scrollBar) || !scrollBar.IsEffectivelyVisible)
        {
            return;
        }

        if (!scrollBar.Classes.Contains(ScrollingClass))
        {
            scrollBar.Classes.Add(ScrollingClass);
        }

        var timer = HideTimers.GetValue(scrollBar, CreateHideTimer);
        timer.Stop();
        timer.Interval = scrollBar.HideDelay;
        timer.Start();
    }

    private static void OnIsEnabledChanged(ScrollBar scrollBar, AvaloniaPropertyChangedEventArgs e)
    {
        if (!e.GetNewValue<bool>())
        {
            Conceal(scrollBar);
        }
    }

    private static DispatcherTimer CreateHideTimer(ScrollBar scrollBar)
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) => Conceal(scrollBar);
        return timer;
    }

    private static void Conceal(ScrollBar scrollBar)
    {
        if (HideTimers.TryGetValue(scrollBar, out var timer))
        {
            timer.Stop();
        }

        scrollBar.Classes.Remove(ScrollingClass);
    }
}
