namespace Lab5Sync;

/// <summary>
/// 5.2.4. Синхронизация потоков с помощью объектов типа «Семафор».
/// 10 потоков (капель), но одновременно «падать» могут не более max_count из них —
/// остальные ждут семафор. Кнопка Sleep усыпляет главный поток функцией Sleep;
/// так как все потоки рисуют через Synchronize, они тоже останавливаются.
///
/// В C# используется System.Threading.Semaphore — объект ядра Windows:
///   CreateSemaphore(nil, max_count, 10, nil) -> new Semaphore(max_count, 10);
///   WaitForSingleObject(h0, 200)             -> sem.WaitOne(200);
///   ReleaseSemaphore(h0, 1, nil)             -> sem.Release();
///   CloseHandle(fsemaphore)                  -> sem.Dispose().
/// </summary>
sealed class SemaphoreForm : LabFormBase
{
    const int ThreadCount = 10;
    const int Column = 64;
    const double StepMs = 1.2;

    sealed class DropState
    {
        public int Frame = 1;       // кадр ведра 1..19 (i в методичке для DBox)
        public int Y = -1;          // координата капли (-1 — капли нет)
        public bool Up;
        public bool Inside;         // поток сейчас «внутри» семафора
    }

    readonly Stage _sky = new() { Dock = DockStyle.Top, Height = 320, BackColor = Color.White };    // PaintBox1 (FBox)
    readonly Stage _buckets = new() { Dock = DockStyle.Top, Height = 54, BackColor = Color.White }; // PaintBox2 (DBox)
    readonly DropState[] _drops = new DropState[ThreadCount];
    readonly NumericUpDown _sleepMs, _maxCount;
    readonly Button _btnSleep = new() { Text = "Sleep", Size = new Size(150, 30) };
    readonly Label _insideLbl;

    Semaphore _semaphore;           // fsemaphore
    int _maxInUse;
    int _inside;

    public SemaphoreForm()
        : base("5.2.4. Синхронизация потоков с помощью объектов типа «Семафор»", 840, 374, 112)
    {
        for (int k = 0; k < ThreadCount; k++) _drops[k] = new DropState();
        StageHost.Controls.Add(_buckets);
        StageHost.Controls.Add(_sky);
        _sky.Paint += SkyPaint;
        _buckets.Paint += BucketsPaint;

        AddLabel("Время приостановки потоков (мс):", 10, 12);
        _sleepMs = AddNumeric(260, 10, 0, 20000, 2000, 100);
        AddLabel("Максимальное кол-во падающих капель:", 10, 44);
        _maxCount = AddNumeric(260, 42, 1, ThreadCount, 7);
        _insideLbl = AddLabel("Сейчас падает капель: 0", 10, 80);

        _btnSleep.Location = new Point(840 - 165, 78);
        _btnSleep.Click += (_, _) => Button1Click();
        ControlsPanel.Controls.Add(_btnSleep);
    }

    static int ColumnX(int k) => 32 + 20 + Column * k;   // numb := 32*(2*numb-1)-16 (+ отступ)

    void SkyPaint(object sender, PaintEventArgs e)
    {
        for (int k = 0; k < ThreadCount; k++)
        {
            var d = _drops[k];
            if (d.Y >= 0) Sprites.Drop(e.Graphics, ColumnX(k), d.Y, d.Up);
        }
    }

    void BucketsPaint(object sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        for (int k = 0; k < ThreadCount; k++)
            Sprites.Bucket(e.Graphics, ColumnX(k), _buckets.Height, _drops[k].Frame, _drops[k].Inside);
    }

    /// <summary>Кнопка Sleep: приостановить главный поток, а вместе с ним все потоки.</summary>
    void Button1Click()
    {
        int ms = (int)_sleepMs.Value;
        Log($"Sleep({ms}) в главном потоке — все потоки ждут Synchronize...");
        FlushLog();
        Thread.Sleep(ms);
        Log("Главный поток проснулся");
    }

    protected override void CreateSyncAndThreads()
    {
        _maxInUse = (int)_maxCount.Value;                          // max_count := SpinEdit1.Value
        _semaphore = new Semaphore(_maxInUse, ThreadCount);        // CreateSemaphore(nil, max_count, 10, nil)
        Log($"Создан семафор: начальный счётчик = {_maxInUse}, максимум = {ThreadCount}");
        _inside = 0;
        for (int k = 0; k < ThreadCount; k++)
        {
            int idx = k;
            _drops[k] = new DropState();
            var t = new AnimThread($"Капля {k + 1}", this, th => Execute(th, idx));
            AddThread(t);
            t.Priority = ThreadPriority.Lowest;                    // tpLowest
        }
        UpdateInside();
    }

    protected override void DestroySync()
    {
        _semaphore?.Dispose();                                     // CloseHandle(fsemaphore)
        _semaphore = null;
        for (int k = 0; k < ThreadCount; k++) _drops[k] = new DropState();
        _inside = 0;
        UpdateInside();
        _sky.Invalidate();                                         // PaintBox1.Repaint
        _buckets.Invalidate();
    }

    void UpdateInside()
    {
        string max = _semaphore == null ? "" : $" (не более {_maxInUse} одновременно)";
        _insideLbl.Text = $"Сейчас падает капель: {Volatile.Read(ref _inside)}{max}";
    }

    /// <summary>DoVisual: показать кадр ведра i и перейти к следующему.</summary>
    void DoVisual(AnimThread t, int k, ref int i)
    {
        var d = _drops[k];
        int frame = i;
        t.Synchronize(() =>
        {
            d.Frame = frame;
            _buckets.Invalidate(new Rectangle(ColumnX(k) - Column / 2, 0, Column, _buckets.Height));
        });
        i = i < 19 ? i + 1 : 1;
    }

    /// <summary>DoVisualSwap: нарисовать каплю в позиции y.</summary>
    void DoVisualSwap(AnimThread t, int k, int y, bool up)
    {
        var d = _drops[k];
        t.Synchronize(() =>
        {
            d.Y = y;
            d.Up = up;
            _sky.Invalidate(new Rectangle(ColumnX(k) - 10, 0, 20, _sky.Height));
        });
    }

    /// <summary>TMoveThread.Execute</summary>
    void Execute(AnimThread t, int k)
    {
        var d = _drops[k];
        int i = 1;
        while (true)
        {
            // ждём семафор; пока ждём — «рябь» в ведре (кадры 1..6)
            while (!t.Wait(_semaphore, 200))
            {
                if (i == 7) i = 1;
                DoVisual(t, k, ref i);
            }
            int now = Interlocked.Increment(ref _inside);
            d.Inside = true;
            Post(UpdateInside);
            Log($"{t.Name}: получила доступ к семафору (падает: {now})");
            try
            {
                i = 1;
                for (int j = 1; j <= 7; j++) { DoVisual(t, k, ref i); t.Sleep(25); }

                int h = _sky.SafeHeight;
                for (int y = h - Sprites.DropH; y > 0; y -= 2)          // вверх
                {
                    DoVisualSwap(t, k, y, true);
                    t.Work(StepMs);
                }
                for (int y = 0; y <= h; y += 2)                         // вниз
                {
                    DoVisualSwap(t, k, y, false);
                    t.Work(StepMs);
                }
                DoVisualSwap(t, k, -1, false);

                i = 8;
                for (int j = 8; j <= 19; j++) { DoVisual(t, k, ref i); t.Sleep(25); }
            }
            finally
            {
                d.Inside = false;
                Interlocked.Decrement(ref _inside);
                _semaphore.Release();                                // ReleaseSemaphore(h0, 1, nil)
                Post(UpdateInside);
                Log($"{t.Name}: освободила семафор (Release)");
            }
        }
    }
}
