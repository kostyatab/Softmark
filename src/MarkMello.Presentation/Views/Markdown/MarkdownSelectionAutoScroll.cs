namespace MarkMello.Presentation.Views.Markdown;

/// <summary>
/// Автопрокрутка при протягивании выделения за край прокручиваемой области —
/// страницы по вертикали или блока кода и таблицы вбок.
/// </summary>
/// <remarks>
/// Расчёт по одной оси в координатах видимой части области: от 0 до её размера.
/// Прокрутка идёт с постоянной скоростью, пока курсор за краем или в полосе у
/// края, и только в ту сторону, где за краем ещё есть содержимое.
/// </remarks>
internal static class MarkdownSelectionAutoScroll
{
    /// <summary>
    /// Скорость прокрутки, DIP в секунду. Взята из постановки и на ощупь в
    /// приложении ещё не проверена.
    /// </summary>
    public const double Speed = 500;

    /// <summary>
    /// Полоса у верхнего и нижнего края страницы, в которой прокрутка уже
    /// начинается. Окно во весь экран кончается вместе с экраном, и курсор не
    /// уходит ниже последней строки пикселей — без полосы страница вниз бы не шла.
    /// </summary>
    public const double PageEdgeZone = 4;

    /// <summary>
    /// Самый длинный шаг по времени: после задержки тика (например, занятого
    /// UI-потока) область не прыгает на экран вперёд.
    /// </summary>
    private static readonly TimeSpan MaxStepDuration = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Куда прокручивать: −1 — назад (вверх или влево), +1 — вперёд, 0 — стоять.
    /// </summary>
    /// <param name="position">Курсор в координатах видимой части.</param>
    /// <param name="viewportSize">Размер видимой части.</param>
    /// <param name="startInset">
    /// Полоса у начального края, в которой прокрутка уже начинается (0 — только за краем).
    /// </param>
    /// <param name="endInset">То же у конечного края.</param>
    /// <param name="offset">Текущее смещение прокрутки.</param>
    /// <param name="maxOffset">Наибольшее смещение прокрутки.</param>
    public static int GetDirection(
        double position,
        double viewportSize,
        double startInset,
        double endInset,
        double offset,
        double maxOffset)
    {
        if (viewportSize <= 0)
        {
            return 0;
        }

        if (position < startInset && offset > 0)
        {
            return -1;
        }

        if (position > viewportSize - endInset && offset < maxOffset)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>Новое смещение после шага прокрутки длительностью <paramref name="elapsed"/>.</summary>
    public static double Step(double offset, double maxOffset, int direction, TimeSpan elapsed)
    {
        if (direction == 0 || maxOffset <= 0)
        {
            return Math.Clamp(offset, 0, Math.Max(0, maxOffset));
        }

        var duration = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > MaxStepDuration ? MaxStepDuration : elapsed;
        return Math.Clamp(offset + direction * Speed * duration.TotalSeconds, 0, maxOffset);
    }

    /// <summary>
    /// Прижимает курсор к видимой части: пока он за краем, конец выделения стоит
    /// на тексте у края, а скрытый текст подтягивает прокрутка.
    /// </summary>
    public static double ClampToViewport(double position, double viewportSize)
        => Math.Clamp(position, 0, Math.Max(0, viewportSize));
}
