namespace Lab5Sync;

/// <summary>
/// 5.2.1. Синхронизация потоков с помощью критических секций.
/// Два потока двигают свои автомобили в ОДНОМ компоненте; критическая секция не допускает
/// одновременного использования компонента: пока один автомобиль едет, второй поток ждёт.
///
/// В C# аналог TCriticalSection — класс Monitor (оператор lock):
///   c.Enter  -> Monitor.Enter(c),   c.Leave -> Monitor.Exit(c).
/// Как и критическая секция Win32, Monitor работает только внутри одного процесса
/// и ждёт без тайм-аута (бесконечно).
/// </summary>
sealed class CriticalSectionForm : LabFormBase
{
    const double StepMs = 1.5;   // процессорное время на один шаг (1 пиксель) движения

    readonly Stage _stage = new() { Dock = DockStyle.Fill, BackColor = Color.Aqua };
    readonly Car[] _cars =
    {
        new("Поток 1 (жёлтое такси)", Color.Gold, 24),
        new("Поток 2 (красное такси)", Color.OrangeRed, 24),
    };
    readonly Label[] _carState = new Label[2];
    readonly AnimThread[] _threads = new AnimThread[2];
    readonly TrackBar[] _prio = new TrackBar[2];

    object _c;                 // критическая секция (общая для обоих потоков)
    volatile int _owner;       // номер потока, находящегося в критической секции (0 — свободна)

    public CriticalSectionForm()
        : base("5.2.1. Синхронизация потоков с помощью критических секций (Monitor / lock)", 820, 100, 150)
    {
        StageHost.Controls.Add(_stage);
        _stage.Paint += StagePaint;

        _prio[0] = AddPriorityTrack("Приоритет потока 1 (жёлтое такси):", 10, 6, () => _threads[0]);
        _prio[1] = AddPriorityTrack("Приоритет потока 2 (красное такси):", 10, 72, () => _threads[1]);
        _carState[0] = AddLabel("Поток 1: не запущен", 360, 30);
        _carState[1] = AddLabel("Поток 2: не запущен", 360, 96);
        PinBox.Location = new Point(360, 6);
        ControlsPanel.Controls.Add(PinBox);
    }

    void StagePaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.FillRectangle(Brushes.DimGray, 0, _stage.Height - 18, _stage.Width, 18);
        foreach (var car in _cars) car.Draw(g, Font);
        string s = _owner == 0 ? "Критическая секция свободна" : $"Критическую секцию занимает поток {_owner}";
        g.DrawString(s, Font, Brushes.Black, 4, 2);
    }

    protected override void CreateSyncAndThreads()
    {
        _c = new object();   // c := TCriticalSection.Create;
        for (int k = 0; k < 2; k++)
        {
            int idx = k;
            _cars[k].X = -1000;
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
        SetCarState(0, "завершён");
        SetCarState(1, "завершён");
        _stage.Invalidate();
    }

    /// <summary>TMoveThread.Execute</summary>
    void Execute(AnimThread t, int k)
    {
        var car = _cars[k];
        int w = Sprites.CarW;
        while (true)
        {
            // едем вправо
            EnterSection(t, k);
            try
            {
                for (int i = -w; i <= _stage.SafeWidth; i += 2)
                {
                    int x = i;
                    t.Synchronize(() => { car.GoingRight = true; car.X = x; _stage.Invalidate(); });   // DoVisualSwap
                    t.Work(StepMs);
                }
            }
            finally { LeaveSection(t, k); }
            t.Sleep(30);   // пауза вне критической секции — даёт второму потоку войти в неё

            // едем влево
            EnterSection(t, k);
            try
            {
                for (int i = _stage.SafeWidth; i >= -w; i -= 2)
                {
                    int x = i;
                    t.Synchronize(() => { car.GoingRight = false; car.X = x; _stage.Invalidate(); });  // DoVisual
                    t.Work(StepMs);
                }
            }
            finally { LeaveSection(t, k); }
            t.Sleep(30);
        }
    }

    void EnterSection(AnimThread t, int k)
    {
        if (!Monitor.TryEnter(_c))
        {
            SetCarState(k, "ЖДЁТ: критическая секция занята другим потоком");
            Log($"{t.Name}: критическая секция занята, ожидание...");
            Monitor.Enter(_c);   // c.Enter — ждём бесконечно, без тайм-аута
        }
        _owner = k + 1;
        SetCarState(k, "в критической секции (едет)");
        Log($"{t.Name}: вошёл в критическую секцию (Enter)");
    }

    void LeaveSection(AnimThread t, int k)
    {
        _owner = 0;
        Monitor.Exit(_c);        // c.Leave — в finally, т.е. освобождается при любом выходе
        SetCarState(k, "вне критической секции");
        Log($"{t.Name}: покинул критическую секцию (Leave)");
    }

    void SetCarState(int k, string s) => Post(() => _carState[k].Text = $"Поток {k + 1}: {s}");
}
