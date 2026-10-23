namespace Lab5Sync;

/// <summary>Главное окно: запуск приложений четырёх заданий практического занятия №5.</summary>
sealed class MainForm : Form
{
    public MainForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Практическое занятие №5 — Средства синхронизации потоков (C#)";
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 400);

        Controls.Add(new Label
        {
            Text = "Средства синхронизации потоков и их реализация (вместо Delphi — C#, WinForms)",
            Location = new Point(20, 15), AutoSize = true, Font = new Font(Font, FontStyle.Bold),
        });

        AddTask(0, "5.2.1. Критическая секция",
            "Два автомобиля в одном компоненте; Monitor (lock) не даёт ехать одновременно.",
            () => new CriticalSectionForm());
        AddTask(1, "5.2.2. Mutex",
            "Второй автомобиль выезжает через заданный интервал (тайм-аут ожидания мьютекса).",
            () => new MutexForm());
        AddTask(2, "5.2.3. Event",
            "Такси ждут события «появился пассажир» и не ездят пустыми.",
            () => new EventForm());
        AddTask(3, "5.2.4. Семафор",
            "10 капель-потоков, одновременно падают не более N (Semaphore).",
            () => new SemaphoreForm());
        ResumeLayout(false);
    }

    void AddTask(int n, string title, string description, Func<Form> create)
    {
        int y = 55 + n * 85;
        var btn = new Button { Text = title, Location = new Point(20, y), Size = new Size(220, 60) };
        btn.Click += (_, _) =>
        {
            var task = create();
            task.FormClosed += (_, _) => Show();   // возврат в главное меню
            Hide();
            task.Show();
        };
        Controls.Add(btn);
        Controls.Add(new Label
        {
            Text = description, Location = new Point(255, y + 8), Size = new Size(345, 50),
        });
    }
}
