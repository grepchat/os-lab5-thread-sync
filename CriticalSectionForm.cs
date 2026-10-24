namespace Lab5Sync;

/// <summary>
/// 5.2.1. Синхронизация потоков с помощью критических секций.
/// Два потока двигают свои автомобили в ОДНОМ компоненте; критическая секция не допускает
/// одновременного использования компонента: пока один автомобиль едет, второй поток ждёт.
///
/// Каждый поток за один вход в критическую секцию проезжает свой участок пути
/// (первый — малый, второй — больший), затем выходит из секции и уступает её другому потоку.
/// Поэтому оба автомобиля видны на дороге, но движутся строго по очереди.
///
/// В C# аналог TCriticalSection — класс Monitor (оператор lock):
///   c.Enter  -> Monitor.Enter(c),   c.Leave -> Monitor.Exit(c).
/// Как и критическая секция Win32, Monitor работает только внутри одного процесса
/// и ждёт без тайм-аута (бесконечно).
/// </summary>
sealed class CriticalSectionForm : LabFormBase
{
    // БЫЛО:
    // const double StepMs = 1.5;   // процессорное время на один шаг (1 пиксель) движения
    // СТАЛО:
    const double StepMs = 8;     // процессорное время на один шаг (2 пикселя) движения

    readonly Stage _stage = new() { Dock = DockStyle.Fill, BackColor = Color.Aqua };
    // БЫЛО: обе машины на одной дорожке
    // readonly Car[] _cars =
    // {
    //     new("Поток 1 (жёлтое такси)", Color.Gold, 24),
    //     new("Поток 2 (красное такси)", Color.OrangeRed, 24),
    // };
    // СТАЛО: каждая машина на своей дорожке того же компонента
    readonly Car[] _cars =
    {
        new("Поток 1 (жёлтое такси)", Color.Gold, 36),
        new("Поток 2 (красное такси)", Color.OrangeRed, 116),
    };
    readonly Label[] _carState = new Label[2];
    readonly AnimThread[] _threads = new AnimThread[2];
    readonly TrackBar[] _prio = new TrackBar[2];
    // БЫЛО: полей _portion и _portionPx не было
    // СТАЛО:
    readonly NumericUpDown[] _portion = new NumericUpDown[2];
    readonly int[] _portionPx = { 80, 240 };   // участок пути за один вход в критическую секцию

    object _c;                 // критическая секция (общая для обоих потоков)
    volatile int _owner;       // номер потока, находящегося в критической секции (0 — свободна)

    // БЫЛО:
    // public CriticalSectionForm()
    //     : base("5.2.1. Синхронизация потоков с помощью критических секций (Monitor / lock)", 820, 100, 150)
    // {
    //     StageHost.Controls.Add(_stage);
    //     _stage.Paint += StagePaint;
    //
    //     _prio[0] = AddPriorityTrack("Приоритет потока 1 (жёлтое такси):", 10, 6, () => _threads[0]);
    //     _prio[1] = AddPriorityTrack("Приоритет потока 2 (красное такси):", 10, 72, () => _threads[1]);
    //     _carState[0] = AddLabel("Поток 1: не запущен", 360, 30);
    //     _carState[1] = AddLabel("Поток 2: не запущен", 360, 96);
    //     PinBox.Location = new Point(360, 6);
    //     ControlsPanel.Controls.Add(PinBox);
    // }
    // СТАЛО: область рисования выше (две дорожки), для каждой машины — поле «Участок пути»
    public CriticalSectionForm()
        : base("5.2.1. Синхронизация потоков с помощью критических секций (Monitor / lock)", 820, 180, 155)
    {
        StageHost.Controls.Add(_stage);
        _stage.Paint += StagePaint;

        for (int k = 0; k < 2; k++)
        {
            int idx = k, y = 6 + 62 * k;
            string name = k == 0 ? "1 (жёлтое такси)" : "2 (красное такси)";
            _prio[k] = AddPriorityTrack($"Приоритет потока {name}:", 10, y, () => _threads[idx]);
            AddLabel("Участок пути за один вход в КС, px:", 360, y + 2);
            _portion[k] = AddNumeric(580, y, 10, 2000, _portionPx[k], 10);
            _portion[k].ValueChanged += (_, _) => Volatile.Write(ref _portionPx[idx], (int)_portion[idx].Value);
            _carState[k] = AddLabel($"Поток {k + 1}: не запущен", 360, y + 30);
            _cars[k].X = 0;
        }
        PinBox.Location = new Point(10, 130);
        ControlsPanel.Controls.Add(PinBox);
    }

    void StagePaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        // БЫЛО: одна дорога внизу
        // g.FillRectangle(Brushes.DimGray, 0, _stage.Height - 18, _stage.Width, 18);
        // foreach (var car in _cars) car.Draw(g, Font);
        // СТАЛО: дорога под каждой машиной
        foreach (var car in _cars)
        {
            g.FillRectangle(Brushes.DimGray, 0, car.Y + Sprites.CarH, _stage.Width, 4);
            car.Draw(g, Font);
        }
        string s = _owner == 0 ? "Критическая секция свободна" : $"Критическую секцию занимает поток {_owner}";
        g.DrawString(s, Font, Brushes.Black, 4, 2);
    }

    protected override void CreateSyncAndThreads()
    {
        _c = new object();   // c := TCriticalSection.Create;
        for (int k = 0; k < 2; k++)
        {
            int idx = k;
            // БЫЛО: машина ждала за левым краем экрана
            // _cars[k].X = -1000;
            // СТАЛО: машина стоит в начале дороги
            _cars[k].X = 0;
            _cars[k].GoingRight = true;
            _cars[k].Tag = null;
            _threads[k] = new AnimThread($"Поток {k + 1}", this, t => Execute(t, idx));
            AddThread(_threads[k]);
            _threads[k].Priority = (ThreadPriority)_prio[k].Value;   // по умолчанию tpLowest
        }
    }

    protected override void DestroySync()
    {
        _c = null;           // c.Free
        _owner = 0;
        _threads[0] = _threads[1] = null;
        foreach (var car in _cars) car.Tag = null;   // СТАЛО: добавлено (надписи над машинами)
        SetCarState(0, "завершён");
        SetCarState(1, "завершён");
        _stage.Invalidate();
    }

    // БЫЛО: за один вход в критическую секцию машина проезжала весь экран
    // /// <summary>TMoveThread.Execute</summary>
    // void Execute(AnimThread t, int k)
    // {
    //     var car = _cars[k];
    //     int w = Sprites.CarW;
    //     while (true)
    //     {
    //         // едем вправо
    //         EnterSection(t, k);
    //         try
    //         {
    //             for (int i = -w; i <= _stage.SafeWidth; i += 2)
    //             {
    //                 int x = i;
    //                 t.Synchronize(() => { car.GoingRight = true; car.X = x; _stage.Invalidate(); });   // DoVisualSwap
    //                 t.Work(StepMs);
    //             }
    //         }
    //         finally { LeaveSection(t, k); }
    //         t.Sleep(30);   // пауза вне критической секции — даёт второму потоку войти в неё
    //
    //         // едем влево
    //         EnterSection(t, k);
    //         try
    //         {
    //             for (int i = _stage.SafeWidth; i >= -w; i -= 2)
    //             {
    //                 int x = i;
    //                 t.Synchronize(() => { car.GoingRight = false; car.X = x; _stage.Invalidate(); });  // DoVisual
    //                 t.Work(StepMs);
    //             }
    //         }
    //         finally { LeaveSection(t, k); }
    //         t.Sleep(30);
    //     }
    // }

    // СТАЛО:
    /// <summary>TMoveThread.Execute: за каждый вход в критическую секцию — свой участок пути.</summary>
    void Execute(AnimThread t, int k)
    {
        var car = _cars[k];
        int x = 0;
        bool right = true;
        while (true)
        {
            EnterSection(t, k);
            try
            {
                int portion = Volatile.Read(ref _portionPx[k]);
                for (int passed = 0; passed < portion; passed += 2)
                {
                    int maxX = _stage.SafeWidth - Sprites.CarW;
                    if (right && x >= maxX) right = false;      // доехали до края — разворот
                    else if (!right && x <= 0) right = true;
                    x += right ? 2 : -2;

                    int xx = x;
                    bool r = right;
                    t.Synchronize(() => { car.GoingRight = r; car.X = xx; _stage.Invalidate(); });   // DoVisualSwap / DoVisual
                    t.Work(StepMs);
                }
                Log($"{t.Name}: проехал участок {portion} px");
            }
            finally { LeaveSection(t, k); }
            t.Sleep(30);   // пауза вне критической секции — даёт второму потоку войти в неё
        }
    }

    void EnterSection(AnimThread t, int k)
    {
        if (!Monitor.TryEnter(_c))
        {
            SetCarState(k, "ЖДЁТ: критическая секция занята другим потоком");
            SetTag(k, "ждёт критическую секцию", Color.Firebrick);   // СТАЛО: добавлено
            Log($"{t.Name}: критическая секция занята, ожидание...");
            Monitor.Enter(_c);   // c.Enter — ждём бесконечно, без тайм-аута
        }
        _owner = k + 1;
        SetCarState(k, "в критической секции (едет)");
        SetTag(k, "в критической секции — едет", Color.DarkGreen);   // СТАЛО: добавлено
        Log($"{t.Name}: вошёл в критическую секцию (Enter)");
    }

    // БЫЛО: запись в журнал делалась после выхода из секции
    // void LeaveSection(AnimThread t, int k)
    // {
    //     _owner = 0;
    //     Monitor.Exit(_c);        // c.Leave — в finally, т.е. освобождается при любом выходе
    //     SetCarState(k, "вне критической секции");
    //     Log($"{t.Name}: покинул критическую секцию (Leave)");
    // }
    // СТАЛО:
    void LeaveSection(AnimThread t, int k)
    {
        _owner = 0;
        SetCarState(k, "вне критической секции");
        SetTag(k, "вне критической секции", Color.DimGray);
        Log($"{t.Name}: покинул критическую секцию (Leave)");   // пишем до выхода, чтобы порядок в журнале был верным
        Monitor.Exit(_c);        // c.Leave — в finally, т.е. освобождается при любом выходе
    }

    void SetCarState(int k, string s) => Post(() => _carState[k].Text = $"Поток {k + 1}: {s}");

    // СТАЛО: новый метод — надпись над машиной
    void SetTag(int k, string tag, Color color) => Post(() =>
    {
        _cars[k].Tag = tag;
        _cars[k].TagColor = color;
        _stage.Invalidate();
    });
}
