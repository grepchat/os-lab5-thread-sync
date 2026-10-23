namespace Lab5Sync;

/// <summary>
/// 5.2.2. Синхронизация потоков с помощью объектов Mutex.
/// Второй автомобиль появляется только через заданный интервал после первого:
/// поток ждёт мьютекс не дольше «временной задержки» (m.Get(time)); если первый автомобиль
/// ещё не освободил мьютекс, по истечении тайм-аута второй всё равно выезжает.
/// После каждого проезда поток приостанавливает себя (Suspend), а таймер его возобновляет.
///
/// В C# используется System.Threading.Mutex — это объект ядра Windows (CreateMutex),
/// поэтому именованный мьютекс "Lab5_Mutex" общий даже для разных процессов:
/// если запустить две копии программы, автомобили в них будут ждать друг друга.
/// </summary>
sealed class MutexForm : LabFormBase
{
    const double StepMs = 1.5;
    const string MutexName = "Lab5_Mutex";

    readonly Stage _stage = new() { Dock = DockStyle.Fill, BackColor = Color.Aqua };
    readonly Car[] _cars =
    {
        new("Поток 1 (красное такси)", Color.OrangeRed, 20),
        new("Поток 2 (оранжевое такси)", Color.Orange, 98),
    };
    readonly AnimThread[] _threads = new AnimThread[2];
    readonly AutoResetEvent[] _resume = { new(false), new(false) };
    readonly bool[] _suspended = new bool[2];
    readonly NumericUpDown _time, _period;
    readonly System.Windows.Forms.Timer _timer = new();

    Mutex _m;
    volatile int _timeMs;

    public MutexForm()
        : base("5.2.2. Синхронизация потоков с помощью объектов Mutex", 820, 160, 110)
    {
        StageHost.Controls.Add(_stage);
        _stage.Paint += StagePaint;

        AddLabel("Временная задержка — тайм-аут ожидания мьютекса m.Get(time), мс:", 10, 10);
        _time = AddNumeric(460, 8, 0, 20000, 1000, 50);
        AddLabel("Период таймера, возобновляющего приостановленные потоки (Resume), мс:", 10, 42);
        _period = AddNumeric(460, 40, 10, 5000, 200, 10);
        PinBox.Location = new Point(10, 76);
        ControlsPanel.Controls.Add(PinBox);

        _timeMs = (int)_time.Value;
        _time.ValueChanged += (_, _) => { _timeMs = (int)_time.Value; Log($"Временная задержка = {_timeMs} мс"); };
        _period.ValueChanged += (_, _) => _timer.Interval = (int)_period.Value;
        _timer.Interval = (int)_period.Value;
        _timer.Tick += TimerTick;
    }

    void StagePaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.FillRectangle(Brushes.DimGray, 0, 76, _stage.Width, 4);
        g.FillRectangle(Brushes.DimGray, 0, 154, _stage.Width, 4);
        foreach (var car in _cars) car.Draw(g, Font);
    }

    /// <summary>Timer1Timer: возобновляет потоки, приостановившие себя после проезда.</summary>
    void TimerTick(object sender, EventArgs e)
    {
        for (int k = 0; k < 2; k++)
            if (_suspended[k]) _resume[k].Set();
    }

    protected override void OnRunStateChanged() => _timer.Enabled = State == RunState.Running;

    protected override void CreateSyncAndThreads()
    {
        _m = new Mutex(false, MutexName);   // m := TMutex.Create('mutex'); изначально никому не принадлежит
        for (int k = 0; k < 2; k++)
        {
            int idx = k;
            _cars[k].X = -1000;
            _cars[k].Tag = null;
            _suspended[k] = false;
            _resume[k].Reset();
            _threads[k] = new AnimThread($"Поток {k + 1}", this, t => Execute(t, idx));
            AddThread(_threads[k]);
            _threads[k].Priority = ThreadPriority.Lowest;
        }
    }

    protected override void DestroySync()
    {
        _m?.Dispose();       // CloseHandle
        _m = null;
        foreach (var c in _cars) c.Tag = null;
        _stage.Invalidate();
    }

    void Execute(AnimThread t, int k)
    {
        var car = _cars[k];
        int w = Sprites.CarW;
        while (true)
        {
            bool own = Get(t, k);
            try
            {
                for (int i = -w; i <= _stage.SafeWidth; i += 2)
                {
                    int x = i;
                    t.Synchronize(() => { car.GoingRight = true; car.X = x; _stage.Invalidate(); });
                    t.Work(StepMs);
                }
            }
            finally { Release(t, k, own); }
            SelfSuspend(t, k);

            own = Get(t, k);
            try
            {
                for (int i = _stage.SafeWidth; i >= -w; i -= 2)
                {
                    int x = i;
                    t.Synchronize(() => { car.GoingRight = false; car.X = x; _stage.Invalidate(); });
                    t.Work(StepMs);
                }
            }
            finally { Release(t, k, own); }
            SelfSuspend(t, k);
        }
    }

    /// <summary>m.Get(time): попытка завладеть мьютексом в течение time мс.</summary>
    bool Get(AnimThread t, int k)
    {
        int time = _timeMs;
        Log($"{t.Name}: ожидает мьютекс (не дольше {time} мс)");
        bool own = t.Wait(_m, time);
        var car = _cars[k];
        if (own)
        {
            Log($"{t.Name}: захватил мьютекс — выезжает");
            Post(() => { car.Tag = "владеет мьютексом"; car.TagColor = Color.DarkGreen; });
        }
        else
        {
            Log($"{t.Name}: тайм-аут {time} мс истёк — выезжает, не дождавшись мьютекса");
            Post(() => { car.Tag = "тайм-аут, без мьютекса"; car.TagColor = Color.Firebrick; });
        }
        return own;
    }

    /// <summary>m.Release: освободить мьютекс (только если поток им владеет).</summary>
    void Release(AnimThread t, int k, bool own)
    {
        if (own)
        {
            _m.ReleaseMutex();
            Log($"{t.Name}: освободил мьютекс (Release)");
        }
        var car = _cars[k];
        Post(() => car.Tag = null);
    }

    /// <summary>Suspend: поток приостанавливает сам себя до срабатывания таймера.</summary>
    void SelfSuspend(AnimThread t, int k)
    {
        _suspended[k] = true;
        t.Wait(_resume[k], Timeout.Infinite);
        _suspended[k] = false;
    }
}
