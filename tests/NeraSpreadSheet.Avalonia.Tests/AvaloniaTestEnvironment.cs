using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

/// <summary>One application/dispatcher per test process; every test still owns fresh
/// windows, controls and workbooks. Assembly cleanup waits for actual thread termination.</summary>
[TestClass]
public sealed class AvaloniaTestEnvironment
{
    private static HeadlessUiThread? _host;

    [AssemblyInitialize]
    public static async Task InitializeAsync(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _host = new HeadlessUiThread();
        await _host.RunAsync(static () => Assert.IsNotNull(Application.Current));
    }

    [AssemblyCleanup]
    public static async Task CleanupAsync()
    {
        if (_host is { } host)
        {
            await host.DisposeAsync();
            _host = null;
            Console.WriteLine("NERA_AVALONIA_HEADLESS_THREAD_STOPPED");
        }
    }

    internal static Task OnUiAsync(Action action) =>
        (_host ?? throw new InvalidOperationException("Assembly UI thread is not initialized.")).RunAsync(action);

    [TestMethod]
    public async Task DispatchShouldKeepOneUiThreadAndPropagateFailures()
    {
        var threadId = 0;
        Application? app = null;
        await OnUiAsync(() => { threadId = Environment.CurrentManagedThreadId; app = Application.Current; });
        var expected = new InvalidOperationException("Intentional dispatch failure");
        try
        {
            await OnUiAsync(() => throw expected);
            Assert.Fail("The dispatch exception must reach the test, not be swallowed.");
        }
        catch (InvalidOperationException actual) { Assert.AreSame(expected, actual); }
        await OnUiAsync(() =>
        {
            Assert.AreEqual(threadId, Environment.CurrentManagedThreadId);
            Assert.AreSame(app, Application.Current);
            Dispatcher.UIThread.VerifyAccess();
        });
    }

    [TestMethod]
    public async Task DispatchShouldDrainPostedUiWorkBeforeReturning()
    {
        var processed = false;
        await OnUiAsync(() => Dispatcher.UIThread.Post(() => processed = true, DispatcherPriority.Background));
        Assert.IsTrue(processed);
    }

    [TestMethod]
    public Task HeadlessHostShouldSurvive100IndependentWindowDraftAndDetachCycles() => OnUiAsync(() =>
    {
        // A fixed workload, never retry-until-green. Any failed cycle fails the test.
        for (var cycle = 0; cycle < 100; cycle++)
        {
            var session = new SpreadsheetSession(new Workbook());
            var original = session.ActiveWorksheet;
            var other = session.Workbook.AddWorksheet("Other");
            using var control = new NeraSpreadsheetControl { Session = session };
            var window = new Window { Width = 800, Height = 500, Content = control };
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.IsTrue(original.GetCell(default).IsEmpty);
                Assert.IsTrue(control.BeginEdit("Discard"));
                session.ActivateWorksheet(other);
                Assert.IsFalse(control.IsEditing);
                Assert.IsFalse(control.CancelEditor());
                Assert.IsTrue(original.GetCell(default).IsEmpty);
                Assert.IsTrue(other.GetCell(default).IsEmpty);
                Assert.IsTrue(control.BeginEdit("Discard on detach"));
                window.Content = null;
                Assert.IsFalse(session.Editor.IsEditing);
                window.Content = control;
                window.UpdateLayout();
                Assert.IsTrue(control.BeginEdit("17"));
                Assert.IsTrue(control.CommitEditor());
                Assert.AreEqual("17", other.GetCell(default).Value.ToString());
                control.Zoom = 1.5;
                Assert.AreEqual(1.5, control.Zoom);
                Assert.IsTrue(session.Undo());
                Assert.IsTrue(other.GetCell(default).IsEmpty);
            }
            finally
            {
                window.Content = null;
                window.Close();
            }
            Assert.IsFalse(window.IsVisible);
        }
    });
}

/// <summary>Uses the public Avalonia manual-headless API rather than the 12.1.2
/// HeadlessUnitTestSession.StartNew self-captured Task.Run task. The thread and all
/// completion signals exist before Start; readiness is signalled by the running pump.</summary>
internal sealed class HeadlessUiThread : IAsyncDisposable
{
    private readonly TaskCompletionSource<Dispatcher> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private int _stopping;

    public HeadlessUiThread()
    {
        _thread = new Thread(ThreadMain) { IsBackground = true, Name = "Nera Avalonia test UI" };
        if (OperatingSystem.IsWindows()) _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public async Task RunAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _stopping) != 0, this);
        var dispatcher = await _ready.Task.ConfigureAwait(false);
        await AwaitOperationAsync(InvokeAsync(dispatcher, action, DispatcherPriority.Normal)).ConfigureAwait(false);
        // Complete already queued control cleanup/text/layout callbacks before the next test.
        await AwaitOperationAsync(InvokeAsync(dispatcher, static () => { }, DispatcherPriority.SystemIdle)).ConfigureAwait(false);
    }

    private async Task AwaitOperationAsync(Task operation)
    {
        await Task.WhenAny(operation, _stopped.Task).ConfigureAwait(false);
        if (!operation.IsCompleted)
        {
            await _stopped.Task.ConfigureAwait(false);
            throw new InvalidOperationException("The UI pump stopped before the queued operation completed.");
        }
        await operation.ConfigureAwait(false);
    }

    private static async Task InvokeAsync(Dispatcher dispatcher, Action action, DispatcherPriority priority) =>
        await dispatcher.InvokeAsync(action, priority);

    private void ThreadMain()
    {
        try
        {
            TestApplication.BuildAvaloniaApp().SetupWithoutStarting();
            var dispatcher = Dispatcher.UIThread;
            dispatcher.Post(() => _ready.TrySetResult(dispatcher));
            dispatcher.MainLoop(CancellationToken.None);
            _stopped.TrySetResult();
        }
        catch (Exception exception)
        {
            // Preserve startup/pump failures for the test and assembly cleanup.
            _ready.TrySetException(exception);
            _stopped.TrySetException(exception);
        }
        finally { SynchronizationContext.SetSynchronizationContext(null); }
    }

    public async ValueTask DisposeAsync()
    {
        if (Environment.CurrentManagedThreadId == _thread.ManagedThreadId)
            throw new InvalidOperationException("The UI thread cannot join itself.");
        if (Interlocked.Exchange(ref _stopping, 1) == 0 && !_stopped.Task.IsCompleted)
        {
            var dispatcher = await _ready.Task.ConfigureAwait(false);
            dispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
        }
        try { await _stopped.Task.ConfigureAwait(false); }
        finally { _thread.Join(); }
    }
}
