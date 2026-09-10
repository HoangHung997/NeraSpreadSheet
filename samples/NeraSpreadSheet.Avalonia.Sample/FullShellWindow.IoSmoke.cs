using global::Avalonia.Controls;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private void CheckIoReady(string phase, Action<string, bool> check)
    {
        check(phase + "-io-input-enabled", !_busy && _split.IsEnabled && _formula.IsEnabled && _tabs.IsEnabled);
        check(phase + "-io-sheet-tabs-enabled", _tabs.Children.Count == Session.Workbook.Worksheets.Count &&
            _tabs.Children.OfType<Button>().All(button => button.IsEnabled));
        check(phase + "-io-save-handler-enabled", _registry.TryResolve("Shell.Save", out _, out var save) && save is not null && save.CanExecute(default));
        check(phase + "-io-save-projection-enabled", _runtime.Snapshot.QuickAccessToolbar.Single(command => command.CommandId.Value == "Shell.Save").IsEnabled);
        check(phase + "-io-save-native-enabled", FindRibbonControl<Button>("ribbon-qat-Shell.Save").IsEnabled);
        check(phase + "-io-format-projection-enabled", _runtime.Snapshot.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Items)
            .Single(item => item.Command.CommandId.Value == "Cell.Bold").Command.IsEnabled);
    }

    private async Task VerifyIoRecoveryAsync(Action<string, bool> check)
    {
        var originalSession = Session;
        var version = Session.ActiveWorksheet.Version;
        var nestedExecutions = 0;
        await RunIo(async () =>
        {
            await SettleRibbonAsync();
            check("busy-input-disabled", _busy && !_split.IsEnabled && !_formula.IsEnabled && !_tabs.IsEnabled);
            check("busy-tabs-disabled", _tabs.Children.OfType<Button>().All(button => !button.IsEnabled));
            check("busy-save-projection-disabled", !_runtime.Snapshot.QuickAccessToolbar.Single(command => command.CommandId.Value == "Shell.Save").IsEnabled);
            check("busy-save-native-disabled", !FindRibbonControl<Button>("ribbon-qat-Shell.Save").IsEnabled);
            await RunIo(() => { nestedExecutions++; return Task.CompletedTask; });
            check("nested-io-not-executed", nestedExecutions == 0);
        });
        await SettleRibbonAsync();
        CheckIoReady("empty-operation", check);

        var expected = new IOException("Synthetic I/O failure for state restoration.");
        Exception? observed = null;
        try { await RunIo(() => Task.FromException(expected)); }
        catch (IOException exception) { observed = exception; }
        check("io-preserves-original-exception", ReferenceEquals(expected, observed));
        await SettleRibbonAsync();
        CheckIoReady("failed-operation", check);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = false;
        try { await RunIo(() => Task.FromCanceled(cancellation.Token)); }
        catch (OperationCanceledException exception) { cancelled = exception.CancellationToken == cancellation.Token; }
        check("io-cancellation-propagated", cancelled);
        await SettleRibbonAsync();
        CheckIoReady("cancelled-operation", check);
        check("io-recovery-no-workbook-mutation", ReferenceEquals(Session, originalSession) && Session.ActiveWorksheet.Version == version);
    }
}
