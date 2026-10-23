namespace Lab5Sync;

/// <summary>
/// 5.2.3. Синхронизация потоков с помощью объектов Event.
/// Автомобили перевозят пассажиров; поездки без пассажира не допускаются.
/// Таймер через каждый временной интервал с заданной вероятностью «создаёт» пассажира:
/// увеличивает count и переводит событие в сигнальное состояние (SetEvent).
/// Поток, у которого нет пассажиров, сбрасывает событие (ResetEvent) и ждёт его (WaitFor).
///
/// В C# аналог TEvent.Create(nil, True, False, ...) — ManualResetEvent(false):
/// событие со сбросом вручную, изначально в несигнальном состоянии.
/// </summary>
sealed class EventForm : LabFormBase
{
    const double StepMs = 1.5;
    const int WaitTimeout = 100000;   // e.WaitFor(100000)
    const int StopWidth = 90;         // ширина остановки справа

    sealed class Taxi
    {
        public Car Car;
        public ManualResetEvent E;     // e: TEvent
        public int Count;              // count — число ожидающих пассажиров
        public volatile bool Waiting;           // стоит на остановке и ждёт событие
    }

    readonly Stage _stage = new() { Dock = DockStyle.Fill, BackColor = Color.Aqua };
    readonly Taxi[] _taxi =
    {
        new() { Car = new Car("Такси 1", Color.Gold, 20) },
        new() { Car = new Car("Такси 2", Color.OrangeRed, 100) },
    };
    readonly AnimThread[] _threads = new AnimThread[2];
    readonly TrackBar[] _prio = new TrackBar[2];
    readonly NumericUpDown[] _prob = new NumericUpDown[2];
    readonly NumericUpDown _interval;
    readonly Label[] _passLbl = new Label[2];
    readonly System.Windows.Forms.Timer _timer = new();
    readonly Random _rnd = new();

    public EventForm()
        : base("5.2.3. Синхронизация потоков с помощью объектов Event", 900, 165, 175)
    {
        StageHost.Controls.Add(_stage);
        _stage.Paint += StagePaint;

        _prio[0] = AddPriorityTrack("Скорость (приоритет) такси 1:", 10, 6, () => _threads[0]);
        _prio[1] = AddPriorityTrack("Скорость (приоритет) такси 2:", 10, 72, () => _threads[1]);

        AddLabel("Вероятность появления пассажира, %:", 370, 8);
        AddLabel("такси 1:", 370, 32);
        _prob[0] = AddNumeric(430, 30, 0, 100, 50, 5);
        AddLabel("такси 2:", 520, 32);
        _prob[1] = AddNumeric(580, 30, 0, 100, 50, 5);
        AddLabel("Временной интервал (мс):", 370, 66);
        _interval = AddNumeric(530, 64, 50, 10000, 1000, 100);
        _passLbl[0] = AddLabel("Пассажиры такси 1: 0", 370, 98);
        _passLbl[1] = AddLabel("Пассажиры такси 2: 0", 540, 98);
        PinBox.Location = new Point(10, 140);
        ControlsPanel.Controls.Add(PinBox);

        _timer.Interval = (int)_interval.Value;
        _interval.ValueChanged += (_, _) => _timer.Interval = (int)_interval.Value;
        _timer.Tick += Timer1Timer;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        foreach (var tx in _taxi)
        {
            tx.Car.GoingRight = false;
            tx.Car.X = _stage.Width - StopWidth - Sprites.CarW;
        }
    }

    void StagePaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        int stopX = _stage.Width - StopWidth;
        g.FillRectangle(Brushes.Khaki, stopX, 0, StopWidth, _stage.Height);
        g.DrawString("Остановка", Font, Brushes.Black, stopX + 10, 2);
        for (int k = 0; k < 2; k++)
        {
            var tx = _taxi[k];
            int roadY = tx.Car.Y + Sprites.CarH;
            g.FillRectangle(Brushes.DimGray, 0, roadY, stopX, 4);

            int n = Volatile.Read(ref tx.Count);
            for (int p = 0; p < Math.Min(n, 5); p++)
                Sprites.Passenger(g, stopX + 6 + p * 15, tx.Car.Y + 22, Color.SaddleBrown);
            if (n > 5) g.DrawString($"+{n - 5}", Font, Brushes.Black, stopX + 50, tx.Car.Y + 46);

            tx.Car.Tag = tx.Waiting ? "ждёт пассажира (WaitFor)" : tx.Car.HasPassenger ? "везёт пассажира" : null;
            tx.Car.TagColor = tx.Waiting ? Color.Firebrick : Color.DarkGreen;
            tx.Car.Draw(g, Font);
        }
    }

    /// <summary>Генерация событий — появление пассажиров.</summary>
    void Timer1Timer(object sender, EventArgs e)
    {
        for (int k = 0; k < 2; k++)
        {
            var tx = _taxi[k];
            if (_rnd.NextDouble() * 100 < (double)_prob[k].Value)
            {
                int n = Interlocked.Increment(ref tx.Count);   // thread.count := thread.count + 1
                tx.E.Set();                                     // thread.e.SetEvent
                Log($"Такси {k + 1}: появился пассажир (ожидают: {n}) — SetEvent");
            }
        }
        UpdatePassengers();
    }

    void UpdatePassengers()
    {
        for (int k = 0; k < 2; k++)
            _passLbl[k].Text = $"Пассажиры такси {k + 1}: {Volatile.Read(ref _taxi[k].Count)}";
        _stage.Invalidate();
    }

    protected override void OnRunStateChanged() => _timer.Enabled = State == RunState.Running;

    protected override void CreateSyncAndThreads()
    {
        for (int k = 0; k < 2; k++)
        {
            int idx = k;
            var tx = _taxi[k];
            tx.E = new ManualResetEvent(false);   // e := TEvent.Create(nil, True, False, ...)
            tx.Count = 0;
            tx.Waiting = false;
            tx.Car.HasPassenger = false;
            tx.Car.GoingRight = false;
            tx.Car.X = _stage.Width - StopWidth - Sprites.CarW;   // такси стоит у остановки
            _threads[k] = new AnimThread($"Такси {k + 1}", this, t => Execute(t, idx));
            AddThread(_threads[k]);
            _threads[k].Priority = (ThreadPriority)_prio[k].Value;
        }
        UpdatePassengers();
    }

    protected override void DestroySync()
    {
        for (int k = 0; k < 2; k++)
        {
            _taxi[k].E?.Dispose();   // e.Free
            _taxi[k].E = null;
            _taxi[k].Waiting = false;
            _threads[k] = null;
        }
        _stage.Invalidate();
    }

    void Execute(AnimThread t, int k)
    {
        var tx = _taxi[k];
        var car = tx.Car;
        while (true)
        {
            WaitPassenger(t, tx);

            // с пассажиром: от остановки влево и обратно
            t.Synchronize(() => { car.HasPassenger = true; _stage.Invalidate(); });
            int right = _stage.SafeWidth - StopWidth - Sprites.CarW;
            for (int i = right; i >= 0; i -= 2)
            {
                int x = i;
                t.Synchronize(() => { car.GoingRight = false; car.X = x; _stage.Invalidate(); });
                t.Work(StepMs);
            }
            for (int i = 0; i <= right; i += 2)
            {
                int x = i;
                t.Synchronize(() => { car.GoingRight = true; car.X = x; _stage.Invalidate(); });
                t.Work(StepMs);
            }
            t.Synchronize(() => { car.HasPassenger = false; car.GoingRight = false; _stage.Invalidate(); });
            Log($"{t.Name}: пассажир доставлен");
        }
    }

    /// <summary>
    /// if count &lt;= 0 then begin e.ResetEvent; e.WaitFor(100000) end else count := count - 1;
    /// Событие сбрасывается ДО проверки count, поэтому SetEvent, сделанный таймером
    /// между проверкой и ожиданием, не теряется.
    /// </summary>
    void WaitPassenger(AnimThread t, Taxi tx)
    {
        while (true)
        {
            tx.E.Reset();                                   // e.ResetEvent
            int c = Volatile.Read(ref tx.Count);
            if (c > 0 && Interlocked.CompareExchange(ref tx.Count, c - 1, c) == c)
            {
                Log($"{t.Name}: пассажир сел (осталось ожидающих: {c - 1})");
                tx.Waiting = false;
                Post(UpdatePassengers);
                return;
            }
            if (!tx.Waiting)
            {
                Log($"{t.Name}: пассажиров нет — ожидание события (WaitFor)");
                tx.Waiting = true;
                Post(_stage.Invalidate);
            }
            if (!t.Wait(tx.E, WaitTimeout))                // e.WaitFor(100000)
                Log($"{t.Name}: тайм-аут {WaitTimeout} мс — продолжает ждать (без пассажира не едет)");
        }
    }
}
