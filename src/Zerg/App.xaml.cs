using System.Windows;
using System.Windows.Threading;
using Zerg.Native;

namespace Zerg;

public partial class App : Application
{
    EventFeed? feed;
    bool showingCrash;
    /// <summary>Another Zerg was already running; this one only told it so.</summary>
    bool second;
    /// <summary>The main window is up. Until it is, a fault is the start failing.</summary>
    bool started;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Read, not yet acted on: a second launch needs the parse it was
        // handed, and only the first says what was wrong with the rest.
        Options? options = null;
        string? wrong = null;
        try
        {
            options = Options.Parse(e.Args);
        }
        catch (ArgumentException x)
        {
            wrong = x.Message;
        }

        // Before the log and the settings: they are the first one's, and a
        // second launch touches neither.
        if (!SingleInstance.Claim())
        {
            second = true;
            SingleInstance.Signal(options?.Open ?? []);
            Shutdown(0);
            return;
        }
        Log.Start();
        // Listened for at once: Explorer opening several files starts a
        // launch per file, together, and each gives up if nobody answers.
        // What they ask is acted on once this start is done (the dispatcher
        // runs it after OnStartup), in the order they came.
        MainViewModel? model = null;
        MainWindow? window = null;
        SingleInstance.Listen(parses => Dispatcher.BeginInvoke(() =>
        {
            if (model is null || window is null) return;
            Log.Write(parses.Count == 0 ? "launched again: brought forward"
                                        : "launched again to open " + string.Join(", ", parses));
            window.ComeForward();
            foreach (var parse in parses) model.Open(parse);
        }));
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, x) => Crash(x.ExceptionObject as Exception, fatal: true);
        TaskScheduler.UnobservedTaskException += (_, x) =>
        {
            Log.Write("UNOBSERVED " + x.Exception);
            x.SetObserved();
        };

        if (options is null)
        {
            TaskDialog.Show("Zerg can't start with those options", wrong + "\n\n" + Options.Usage,
                            TaskDialog.Icon.Warning);
            Shutdown(2);
            return;
        }

        var settings = Settings.Load();
        // The runs' colours first: the theme's first set is then made with them.
        AppTheme.Runs(settings.RunA, settings.RunB);
        AppTheme.Apply(settings.ThemeChoice);

        var eventsDir = options.EventsDir ?? MainViewModel.FolderIn(settings);
        Log.Write($"events dir {eventsDir}{(options.EventsDir != null ? " (--events-dir)" : "")}; theme {settings.Theme}");

        feed = new EventFeed(eventsDir);
        model = new MainViewModel(settings, feed);
        window = new MainWindow(model, settings);
        MainWindow = window;
        window.Show();
        started = true;
        // The panels that were out last time, now there is a window to open them beside.
        window.Reopen();
        feed.Start();
        foreach (var parse in options.Open)
        {
            Log.Write($"started to open {parse}");
            model.Open(parse);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (second)
        {
            base.OnExit(e);
            return;
        }
        feed?.Dispose();
        Log.Write("exit");
        base.OnExit(e);
    }

    /// <summary>An exception on the UI thread is logged and shown, and Zerg
    /// carries on: most are one bad poll or one bad click, and an app that
    /// quits mid-fight loses the fight's numbers. Before the main window is
    /// up there is no fight to lose and nothing to carry on with: a Zerg left
    /// running with no window keeps the single-instance claim, and every
    /// later launch would signal it and quit without a word.</summary>
    void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        if (!started)
        {
            Crash(e.Exception, fatal: true);
            Shutdown(1);
            return;
        }
        Crash(e.Exception, fatal: false);
    }

    void Crash(Exception? e, bool fatal)
    {
        Log.Write((fatal ? "FATAL " : "UNHANDLED ") + e);
        // A fault in something that runs many times a second would otherwise
        // stack a dialog on every tick while the first one is still open.
        if (showingCrash || e is null) return;
        showingCrash = true;
        try
        {
            TaskDialog.Show(
                fatal ? "Zerg has to close" : "Zerg ran into a problem",
                e.Message + (fatal ? "" : "\n\nZerg will carry on. If something looks wrong, restart it."),
                TaskDialog.Icon.Error,
                details: e.ToString(),
                footer: "Details are in " + Log.FilePath,
                owner: fatal ? null : MainWindow is { IsVisible: true } w ? w : null);
        }
        finally
        {
            showingCrash = false;
        }
    }
}
