using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lab5Sync;

/// <summary>Сигнал завершения потока: бросается внутри тела потока, чтобы раскрутить
/// стек через блоки finally (где освобождаются объекты синхронизации).</summary>
sealed class StopSignal : Exception { }

/// <summary>
/// Аналог TThread из Delphi:
///  Execute        -> делегат body;
///  Synchronize    -> Control.Invoke (выполнение кода в главном потоке);
///  Suspend/Resume -> Pause/Resume (через «ворота» ManualResetEvent, т.к. Thread.Suspend в .NET запрещён);
///  Terminate      -> RequestStop.
/// </summary>
sealed class AnimThread
{
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentThread();
    [DllImport("kernel32.dll")] static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr mask);

    readonly Thread _thread;
    readonly Control _ui;
    readonly Action<AnimThread> _body;
    readonly ManualResetEvent _runGate = new(true);   // открыт = поток работает
    readonly ManualResetEvent _stopEvent = new(false);
    readonly WaitHandle[] _gateOrStop;
    volatile bool _stop;

    public string Name { get; }
    /// <summary>Привязать поток к одному ядру ЦП, чтобы потоки конкурировали за процессор
    /// и влияние приоритета было видно (на многоядерном ЦП иначе почти незаметно).</summary>
    public bool PinToOneCore { get; set; }
    public Action<string> Log { get; set; }

    public AnimThread(string name, Control ui, Action<AnimThread> body)
    {
        Name = name;
        _ui = ui;
        _body = body;
        _gateOrStop = new WaitHandle[] { _runGate, _stopEvent };
        _thread = new Thread(Run) { IsBackground = true, Name = name };
    }

    public ThreadPriority Priority
    {
        get => _thread.Priority;
        set { try { _thread.Priority = value; } catch (ThreadStateException) { } }
    }

    public void Start() => _thread.Start();
    public void Pause() => _runGate.Reset();
    public void Resume() => _runGate.Set();

    public void RequestStop()
    {
        _stop = true;
        _stopEvent.Set();
        _runGate.Set();
    }

    public bool Join(int ms) => _thread.Join(ms);

    void Run()
    {
        if (PinToOneCore) SetThreadAffinityMask(GetCurrentThread(), (UIntPtr)1);
        try { _body(this); }
        catch (StopSignal) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        finally { Log?.Invoke($"{Name}: поток завершён"); }
    }

    /// <summary>Точка проверки: ждём, если поток приостановлен; выходим, если его завершают.</summary>
    public void CheckPoint()
    {
        if (!_stop) WaitHandle.WaitAny(_gateOrStop);
        if (_stop) throw new StopSignal();
    }

    /// <summary>Аналог TThread.Synchronize: выполнить действие в главном (UI) потоке.</summary>
    public void Synchronize(Action action)
    {
        CheckPoint();
        try { _ui.Invoke(action); }
        catch (ObjectDisposedException) { throw new StopSignal(); }
        catch (InvalidOperationException) { throw new StopSignal(); }
    }

    /// <summary>Функция ожидания (аналог WaitForSingleObject) с учётом запроса на завершение.
    /// true — объект перешёл в сигнальное состояние (для мьютекса/семафора — захвачен),
    /// false — истёк тайм-аут.</summary>
    public bool Wait(WaitHandle handle, int timeoutMs)
    {
        int r;
        try { r = WaitHandle.WaitAny(new[] { handle, _stopEvent }, timeoutMs); }
        catch (AbandonedMutexException e) when (e.MutexIndex == 0) { return true; }
        if (r == 0) return true;
        if (r == 1) throw new StopSignal();
        return false;
    }

    /// <summary>Ожидание любого из объектов (аналог WaitForMultipleObjects):
    /// индекс сработавшего объекта или -1 по тайм-ауту.</summary>
    public int WaitAny(int timeoutMs, params WaitHandle[] handles)
    {
        var all = new WaitHandle[handles.Length + 1];
        handles.CopyTo(all, 0);
        all[^1] = _stopEvent;
        int r = WaitHandle.WaitAny(all, timeoutMs);
        if (r == handles.Length) throw new StopSignal();
        return r == WaitHandle.WaitTimeout ? -1 : r;
    }

    public void Sleep(int ms)
    {
        if (_stopEvent.WaitOne(ms)) throw new StopSignal();
    }

    // ---- «работа» потока: фиксированный объём вычислений на один шаг анимации ----
    // Скорость движения определяется тем, сколько процессорного времени получает поток,
    // поэтому от приоритета зависит, как быстро едет автомобиль.

    static readonly double ItersPerMs = Calibrate();
    static double _sink;

    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    static double Spin(long n)
    {
        double x = 0;
        for (long k = 1; k <= n; k++) x += 1.0 / k;
        return x;
    }

    static double Calibrate()
    {
        Spin(100_000);
        const long n = 3_000_000;
        var sw = Stopwatch.StartNew();
        _sink = Spin(n);
        return n / Math.Max(sw.Elapsed.TotalMilliseconds, 0.1);
    }

    public void Work(double ms) => _sink = Spin((long)(ItersPerMs * ms));
}
