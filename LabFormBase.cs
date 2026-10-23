namespace Lab5Sync;

/// <summary>
/// Общая часть всех четырёх заданий: область анимации, панель параметров,
/// кнопки Start / Finish и журнал событий.
///
/// Логика кнопок (как в методичке):
///   Start  — создать и запустить потоки; если потоки приостановлены — возобновить их;
///   Finish — первое нажатие приостанавливает потоки, второе — завершает их
///            и освобождает объект синхронизации.
/// </summary>
abstract class LabFormBase : Form
{
    protected enum RunState { Stopped, Running, Paused, Stopping }

    protected RunState State { get; private set; } = RunState.Stopped;
    protected readonly List<AnimThread> Threads = new();

    protected readonly Panel StageHost = new() { Dock = DockStyle.Top };
    protected readonly Panel ControlsPanel = new() { Dock = DockStyle.Top };
    protected readonly Button BtnStart = new() { Text = "Start", Size = new Size(150, 30) };
    protected readonly Button BtnFinish = new() { Text = "Finish", Size = new Size(150, 30) };
    protected readonly CheckBox PinBox = new()
    {
        Text = "Все потоки на одном ядре ЦП (наглядность приоритетов)",
        AutoSize = true,
        Checked = true,
    };
    readonly ListBox _log = new() { Dock = DockStyle.Fill, Font = new Font("Consolas", 9f), IntegralHeight = false };
    readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolTip _tip = new();
    bool _closeAfterStop;

    protected LabFormBase(string title, int width, int stageHeight, int controlsHeight)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = title;
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(width, stageHeight + controlsHeight + 190 + 25);

        StageHost.Height = stageHeight;
        ControlsPanel.Height = controlsHeight;
        ControlsPanel.BorderStyle = BorderStyle.FixedSingle;

        BtnStart.Location = new Point(width - 165, 8);
        BtnFinish.Location = new Point(width - 165, 44);
        BtnStart.Click += (_, _) => StartClick();
        BtnFinish.Click += async (_, _) => await FinishClick();
        ControlsPanel.Controls.Add(BtnStart);
        ControlsPanel.Controls.Add(BtnFinish);

        var logGroup = new GroupBox { Text = "Журнал событий", Dock = DockStyle.Fill, Padding = new Padding(6) };
        logGroup.Controls.Add(_log);
        var strip = new StatusStrip { SizingGrip = false };
        strip.Items.Add(_status);

        // возврат в главное меню: закрываем окно задания (потоки при этом завершаются),
        // а главное окно снова показывается по событию FormClosed (см. MainForm)
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var back = new ToolStripButton("← Главное меню") { ToolTipText = "Завершить потоки и вернуться в главное меню" };
        back.Click += (_, _) => Close();
        toolbar.Items.Add(back);

        // порядок добавления важен для докинга: Fill — первым, верхние панели — последними
        Controls.Add(logGroup);
        Controls.Add(strip);
        Controls.Add(ControlsPanel);
        Controls.Add(StageHost);
        Controls.Add(toolbar);
        ResumeLayout(false);

        UpdateButtons();
    }

    // ---------- то, что реализует каждое задание ----------

    /// <summary>Создать объект синхронизации и потоки (добавить их в Threads).</summary>
    protected abstract void CreateSyncAndThreads();

    /// <summary>Освободить (закрыть) объект синхронизации после завершения потоков.</summary>
    protected abstract void DestroySync();

    /// <summary>Вызывается при каждом изменении состояния (для таймеров и т.п.).</summary>
    protected virtual void OnRunStateChanged() { }

    // ---------- вспомогательные методы для размещения элементов ----------

    protected Label AddLabel(string text, int x, int y)
    {
        var l = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
        ControlsPanel.Controls.Add(l);
        return l;
    }

    protected NumericUpDown AddNumeric(int x, int y, int min, int max, int value, int increment = 1)
    {
        var n = new NumericUpDown
        {
            Location = new Point(x, y), Width = 80,
            Minimum = min, Maximum = max, Value = value, Increment = increment,
        };
        ControlsPanel.Controls.Add(n);
        return n;
    }

    protected static readonly string[] PriorityNames =
        { "Lowest (tpLowest)", "BelowNormal (tpLower)", "Normal (tpNormal)", "AboveNormal (tpHigher)", "Highest (tpHighest)" };

    /// <summary>TrackBar приоритета потока + подпись с текущим значением.</summary>
    protected TrackBar AddPriorityTrack(string caption, int x, int y, Func<AnimThread> thread)
    {
        AddLabel(caption, x, y);
        var tb = new TrackBar
        {
            Location = new Point(x, y + 18), Width = 180,
            Minimum = 0, Maximum = 4, Value = 0, LargeChange = 1, TickStyle = TickStyle.BottomRight,
        };
        var val = AddLabel(PriorityNames[0], x + 185, y + 24);
        tb.ValueChanged += (_, _) =>
        {
            val.Text = PriorityNames[tb.Value];
            var t = thread();
            if (t != null)
            {
                t.Priority = (ThreadPriority)tb.Value;
                Log($"{t.Name}: приоритет = {PriorityNames[tb.Value]}");
            }
        };
        ControlsPanel.Controls.Add(tb);
        return tb;
    }

    // ---------- журнал ----------

    protected void Log(string msg)
    {
        if (IsDisposed || !IsHandleCreated) return;
        if (InvokeRequired)
        {
            try { BeginInvoke(() => Log(msg)); } catch (InvalidOperationException) { }
            return;
        }
        _log.Items.Add($"{DateTime.Now:HH:mm:ss.fff}  {msg}");
        if (_log.Items.Count > 1000) _log.Items.RemoveAt(0);
        _log.TopIndex = _log.Items.Count - 1;
    }

    /// <summary>Выполнить действие в UI-потоке асинхронно (без ожидания).</summary>
    protected void Post(Action a)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); } catch (InvalidOperationException) { }
    }

    protected void FlushLog() => _log.Update();

    // ---------- управление потоками ----------

    protected void AddThread(AnimThread t)
    {
        t.Log = Log;
        t.PinToOneCore = PinBox.Checked && PinBox.Parent != null;
        Threads.Add(t);
    }

    void StartClick()
    {
        if (State == RunState.Stopped)
        {
            Threads.Clear();
            CreateSyncAndThreads();
            foreach (var t in Threads) t.Start();
            SetState(RunState.Running);
            Log($"Создано и запущено потоков: {Threads.Count}");
        }
        else if (State == RunState.Paused)
        {
            foreach (var t in Threads) t.Resume();
            SetState(RunState.Running);
            Log("Потоки возобновлены (Resume)");
        }
    }

    async Task FinishClick()
    {
        if (State == RunState.Running)
        {
            foreach (var t in Threads) t.Pause();
            SetState(RunState.Paused);
            Log("Потоки приостановлены (Suspend). Повторное нажатие Finish завершит их");
        }
        else if (State == RunState.Paused)
        {
            await StopAllAsync();
        }
    }

    protected async Task StopAllAsync()
    {
        SetState(RunState.Stopping);
        var all = Threads.ToArray();
        foreach (var t in all) t.RequestStop();
        // ждём в фоне, чтобы главный поток продолжал обрабатывать Invoke от рабочих потоков
        await Task.Run(() => { foreach (var t in all) t.Join(5000); });
        Threads.Clear();
        DestroySync();
        Log("Все потоки завершены, объект синхронизации освобождён");
        SetState(RunState.Stopped);
        if (_closeAfterStop) Close();
    }

    void SetState(RunState s)
    {
        State = s;
        UpdateButtons();
        OnRunStateChanged();
    }

    void UpdateButtons()
    {
        BtnStart.Enabled = State is RunState.Stopped or RunState.Paused;
        BtnFinish.Enabled = State is RunState.Running or RunState.Paused;
        BtnStart.Text = State == RunState.Paused ? "Start (возобновить)" : "Start";
        BtnFinish.Text = State == RunState.Paused ? "Finish (завершить)" : "Finish";
        _tip.SetToolTip(BtnFinish, State == RunState.Paused ? "Завершение потоков" : "Остановка потоков");
        PinBox.Enabled = State == RunState.Stopped;
        _status.Text = State switch
        {
            RunState.Stopped => "Потоки не запущены",
            RunState.Running => $"Потоки работают ({Threads.Count})",
            RunState.Paused => "Потоки приостановлены",
            _ => "Завершение потоков...",
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (State != RunState.Stopped)
        {
            e.Cancel = true;
            bool stopping = _closeAfterStop || State == RunState.Stopping;
            _closeAfterStop = true;
            if (!stopping) _ = StopAllAsync();
            return;
        }
        base.OnFormClosing(e);
    }
}
