namespace Lab5Sync;

/// <summary>Состояние одного автомобиля на форме (изменяется только в главном потоке через Synchronize).</summary>
sealed class Car
{
    public readonly string Name;
    public readonly Bitmap Right, Left;   // b0, b1 из методички: картинка «вправо» и «влево»
    public int X = -1000;                 // i из методички
    public int Y;
    public bool GoingRight = true;
    public string Tag;                    // подпись над автомобилем
    public Color TagColor = Color.Black;
    public bool HasPassenger;

    public Car(string name, Color color, int y)
    {
        Name = name;
        Right = Sprites.Taxi(color, true);
        Left = Sprites.Taxi(color, false);
        Y = y;
    }

    public void Draw(Graphics g, Font font)
    {
        g.DrawImage(GoingRight ? Right : Left, X, Y);
        if (HasPassenger)
        {
            int px = GoingRight ? X + 45 : X + Sprites.CarW - 55;
            g.FillEllipse(Brushes.SaddleBrown, px, Y + 12, 8, 8);
            g.FillRectangle(Brushes.SaddleBrown, px, Y + 19, 8, 4);
        }
        if (Tag != null)
        {
            using var br = new SolidBrush(TagColor);
            g.DrawString(Tag, font, br, X, Y - 16);
        }
    }
}
