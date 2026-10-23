using System.Drawing.Drawing2D;

namespace Lab5Sync;

/// <summary>Графические элементы (вместо taxi00.bmp, kaplya.bmp и т.п. рисуются программно).</summary>
static class Sprites
{
    public const int CarW = 120, CarH = 56;

    static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Такси, смотрящее вправо (facingRight) или влево.</summary>
    public static Bitmap Taxi(Color body, bool facingRight)
    {
        var bmp = new Bitmap(CarW, CarH);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(body);
            using var outline = new Pen(Color.FromArgb(70, 40, 0), 1.5f);
            using var glass = new SolidBrush(Color.FromArgb(170, 220, 255));

            Point[] cabin = { new(28, 23), new(40, 9), new(82, 9), new(97, 23) };
            g.FillPolygon(fill, cabin);
            g.DrawPolygon(outline, cabin);
            g.FillPolygon(glass, new Point[] { new(34, 22), new(43, 12), new(59, 12), new(59, 22) });
            g.FillPolygon(glass, new Point[] { new(63, 22), new(63, 12), new(80, 12), new(90, 22) });

            using (var p = RoundedRect(new Rectangle(3, 22, 114, 21), 8))
            {
                g.FillPath(fill, p);
                g.DrawPath(outline, p);
            }
            for (int x = 10, k = 0; x < 110; x += 6, k++)
                g.FillRectangle(k % 2 == 0 ? Brushes.Black : Brushes.White, x, 29, 6, 4);

            g.FillEllipse(Brushes.LightYellow, 108, 25, 8, 6);
            g.FillRectangle(Brushes.Red, 3, 26, 4, 5);

            foreach (int cx in new[] { 30, 90 })
            {
                g.FillEllipse(Brushes.Black, cx - 10, 35, 20, 20);
                g.FillEllipse(Brushes.Silver, cx - 5, 40, 10, 10);
            }
        }
        if (!facingRight) bmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        using (var g = Graphics.FromImage(bmp))
        {
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.FillRectangle(Brushes.Yellow, 47, 1, 26, 8);
            g.DrawRectangle(Pens.Black, 47, 1, 26, 8);
            using var f = new Font("Arial", 5.5f, FontStyle.Bold);
            g.DrawString("TAXI", f, Brushes.Black, 48, 0);
        }
        return bmp;
    }

    /// <summary>Человечек-пассажир (x,y — левый верхний угол, рост ~22 px).</summary>
    public static void Passenger(Graphics g, int x, int y, Color color)
    {
        using var pen = new Pen(color, 2.5f);
        using var br = new SolidBrush(color);
        g.FillEllipse(br, x + 2, y, 7, 7);
        g.DrawLine(pen, x + 5, y + 7, x + 5, y + 15);
        g.DrawLine(pen, x + 5, y + 9, x, y + 13);
        g.DrawLine(pen, x + 5, y + 9, x + 10, y + 13);
        g.DrawLine(pen, x + 5, y + 15, x + 1, y + 22);
        g.DrawLine(pen, x + 5, y + 15, x + 9, y + 22);
    }

    public const int DropW = 14, DropH = 20;

    /// <summary>Капля. up=true — летит вверх (острый конец снизу), иначе падает (острый конец сверху).</summary>
    public static void Drop(Graphics g, int cx, int y, bool up)
    {
        using var p = new GraphicsPath();
        if (up)
        {
            p.AddArc(cx - DropW / 2, y, DropW, DropW, 180, 180);
            p.AddLine(cx + DropW / 2, y + DropW / 2, cx, y + DropH);
            p.AddLine(cx, y + DropH, cx - DropW / 2, y + DropW / 2);
        }
        else
        {
            p.AddLine(cx, y, cx + DropW / 2, y + DropH - DropW / 2);
            p.AddArc(cx - DropW / 2, y + DropH - DropW, DropW, DropW, 0, 180);
            p.AddLine(cx - DropW / 2, y + DropH - DropW / 2, cx, y);
        }
        p.CloseFigure();
        g.FillPath(Brushes.DeepSkyBlue, p);
        g.DrawPath(Pens.SteelBlue, p);
    }

    /// <summary>
    /// Ведро с водой, кадр 1..19 (аналог картинок 1.bmp..19.bmp):
    /// 1..6 — рябь (ожидание), 7 — капля выходит из воды, 8..19 — всплеск после возвращения капли.
    /// </summary>
    public static void Bucket(Graphics g, int cx, int height, int frame, bool active)
    {
        int top = 8, bottom = height - 4;
        Point[] shape = { new(cx - 20, top), new(cx + 20, top), new(cx + 16, bottom), new(cx - 16, bottom) };
        using (var body = new SolidBrush(active ? Color.FromArgb(70, 110, 220) : Color.FromArgb(60, 70, 150)))
            g.FillPolygon(body, shape);

        int water = top + 10;
        using (var wp = new GraphicsPath())
        {
            var pts = new List<PointF>();
            for (int x = -17; x <= 17; x += 2)
            {
                double amp = frame <= 7 ? 1.5 : Math.Max(0, 4 - (frame - 8) * 0.35);
                pts.Add(new PointF(cx + x, (float)(water + amp * Math.Sin(x * 0.5 + frame * 1.1))));
            }
            wp.AddLines(pts.ToArray());
            wp.AddLine(cx + 17, water, cx + 15, bottom - 2);
            wp.AddLine(cx + 15, bottom - 2, cx - 15, bottom - 2);
            wp.CloseFigure();
            g.FillPath(Brushes.Cyan, wp);
        }
        g.DrawPolygon(Pens.Navy, shape);

        if (frame == 7)
            g.FillEllipse(Brushes.DeepSkyBlue, cx - 5, water - 9, 10, 10);
        if (frame >= 8)
        {
            int r = (frame - 7) * 2;
            int alpha = Math.Max(30, 255 - (frame - 8) * 20);
            using var pen = new Pen(Color.FromArgb(alpha, Color.DeepSkyBlue), 2);
            g.DrawArc(pen, cx - r, water - r / 2 - 2, 2 * r, r, 180, 180);
            for (int s = -1; s <= 1; s += 2)
                g.FillEllipse(Brushes.DeepSkyBlue, cx + s * (4 + r), water - 4 - Math.Max(0, 10 - r), 4, 4);
        }
    }
}
