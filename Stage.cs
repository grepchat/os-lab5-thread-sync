namespace Lab5Sync;

/// <summary>Аналог TPaintBox: поверхность для рисования с двойной буферизацией.</summary>
sealed class Stage : Control
{
    volatile int _w, _h;

    public Stage()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    /// <summary>Размеры, безопасные для чтения из рабочих потоков.</summary>
    public int SafeWidth => _w;
    public int SafeHeight => _h;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _w = ClientSize.Width;
        _h = ClientSize.Height;
    }
}
