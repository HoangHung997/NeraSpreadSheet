using System.Diagnostics;
using System.Text.Json;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Input;
using global::Avalonia.Input.Raw;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    /// <summary>Runs additional Formula UX assertions in a real native window.
    /// Input is routed programmatically; this is not physical keyboard/mouse proof.</summary>
    internal void StartFormulaSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var deadline = Stopwatch.StartNew();
        _smokeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _smokeTimer.Tick += async (_, _) =>
        {
            if (_split.ActiveSpreadsheet.RenderedFrameCount == 0 && deadline.Elapsed < TimeSpan.FromSeconds(15)) return;
            _smokeTimer?.Stop();
            var checks = new List<string>();
            void Check(string id, bool condition)
            {
                if (!condition) throw new InvalidOperationException("Formula UX native check failed: " + id);
                if (checks.Contains(id)) throw new InvalidOperationException("Duplicate native check: " + id);
                checks.Add(id);
            }
            try
            {
                var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
                Check("exact-source", sha is { Length: 40 } && sha.All(Uri.IsHexDigit));
                Check("loaded-native-window", _split.ActiveSpreadsheet.RenderedFrameCount > 0 && IsVisible);
                var workbook = new Workbook(); var first = workbook.Worksheets[0];
                var other = workbook.AddWorksheet("O'Brien");
                other.SetValue(new CellAddress(1, 2), 10d); other.SetValue(new CellAddress(2, 2), 20d);
                var session = new SpreadsheetSession(workbook);
                _split.Session = session; _split.SetMode(SpreadsheetSplitViewMode.Vertical); _split.SetZoom(1); UpdateLayout();
                var left = _split.GetPane(SpreadsheetSplitViewPane.TopLeft);
                var right = _split.GetPane(SpreadsheetSplitViewPane.TopRight);
                var columns = first.Dimensions.DefaultColumnWidth; var rows = first.Dimensions.DefaultRowHeight;
                Point Local(double column, double row) => new(left.RenderTheme.RowHeaderWidth + column * columns, left.RenderTheme.ColumnHeaderHeight + row * rows);

                left.BeginEdit("=SUM(B2:B4,C2:C4,");
                Check("incomplete-draft-ranges", left.CurrentFormulaReferenceHighlights.Count == 2);
                var color = left.CurrentFormulaReferenceHighlights[0].Color;
                Check("unchanged-draft-preview", left.EditorText == "=SUM(B2:B4,C2:C4," && first.GetCell(default).IsEmpty);
                left.InsertFormulaReference(new CellRange(new CellAddress(1, 3), new CellAddress(3, 3)));
                Check("stable-reference-colors", left.CurrentFormulaReferenceHighlights.Count == 3 && left.CurrentFormulaReferenceHighlights[0].Color == color);
                var frames = left.RenderedFrameCount;
                UpdateLayout(); CaptureNative(this, "formula-ux-references.png");
                Check("formula-capture-render", left.RenderedFrameCount > frames);
                left.CancelEditor();

                right.ScrollTo(columns * 10 + 0.25, rows * 10 + 0.75);
                var selection = session.Selection.Capture().Version; var sheetVersion = first.Version;
                left.BeginEdit("=");
                using (var gesture = new NativeFormulaGesture(this, left, Local(1.5, 1.5)))
                {
                    gesture.Move(right, Local(1.5, 2.5)); gesture.Release(right, Local(1.5, 2.5));
                }
                Check("same-pane-start-cross-pane-drag", left.EditorText == "=B2:L13");
                Check("cross-pane-draft-ownership", ReferenceEquals(_split.EditingSpreadsheet, left) && _split.State.ActivePane == SpreadsheetSplitViewPane.TopLeft && session.Selection.Capture().Version == selection);
                left.CancelEditor(); left.BeginEdit("=");
                using (var gesture = new NativeFormulaGesture(this, right, Local(1.5, 1.5)))
                {
                    gesture.Move(left, Local(2.5, 2.5)); gesture.Release(left, Local(2.5, 2.5));
                }
                Check("reverse-cross-pane-drag", left.EditorText == "=C3:L12");
                left.CancelEditor(); right.ScrollTo(0, 0); left.ScrollTo(0, 0); left.BeginEdit("=");
                var edge = new Point(right.Bounds.Width - 7.25, 160);
                var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnScrolled(object? sender, EventArgs e)
                {
                    if (right.ScrollSnapshot.OffsetX > 0) observed.TrySetResult();
                }
                right.ViewportChanged += OnScrolled;
                try
                {
                    using var gesture = new NativeFormulaGesture(this, left, Local(1.5, 1.5));
                    gesture.Move(right, edge);
                    // Await the actual production frame callback, not a test-only AdvanceFrame API.
                    await observed.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    Check("real-frame-edge-autoscroll", right.ScrollSnapshot.OffsetX > 0 && left.IsFormulaPointMode);
                    Check("hovered-pane-only-scroll", left.ScrollSnapshot.OffsetX == 0 && _split.State.ActivePane == SpreadsheetSplitViewPane.TopLeft);
                    gesture.Release(right, edge);
                    Check("release-stops-point-mode", !left.IsFormulaPointMode && left.IsEditing);
                }
                finally { right.ViewportChanged -= OnScrolled; }
                left.CancelEditor(); left.ScrollTo(0, 0); right.ScrollTo(0, 0);

                left.BeginEdit("=$B$2:C3+D5");
                using (var gesture = new NativeFormulaGesture(this, left, Local(1, 1.5)))
                {
                    gesture.Move(left, Local(2, 2.5)); gesture.Release(left, Local(2, 2.5));
                }
                Check("border-move-token-only", left.EditorText == "=$C$3:D4+D5" && !left.IsDraggingFormulaReferenceBorder);
                left.CancelEditor(); left.BeginEdit("=B2:C3+D5");
                using (var gesture = new NativeFormulaGesture(this, left, Local(3, 3) - new Vector(0.2, 0.2)))
                {
                    gesture.Move(left, Local(4.5, 4.5)); gesture.Release(left, Local(4.5, 4.5));
                }
                Check("border-resize", left.EditorText == "=B2:E5+D5");
                Check("gesture-no-workbook-edit", first.Version == sheetVersion && first.GetCell(default).IsEmpty && !session.Undo());
                left.CancelEditor(); left.BeginEdit("=SUM(");
                var edit = session.Editor.State;
                ShowFormulaReferencePicker(other);
                var pickerWindow = _dialogs.Last();
                var picker = (NeraFormulaReferencePicker)pickerWindow.Content!;
                pickerWindow.UpdateLayout();
                Check("picker-native-open", pickerWindow.IsVisible && ReferenceEquals(picker.SelectedWorksheet, other));
                Check("picker-source-session-stable", ReferenceEquals(session.ActiveWorksheet, first) && ReferenceEquals(session.Editor.State, edit) && left.EditorText == "=SUM(");
                // The preview surface is deliberately read-only. Resolve its actual native
                // visual, then exercise the same routed selection input as a consumer.
                var preview = picker.GetVisualDescendants().OfType<Control>()
                    .Single(control => control.GetType().Name == "FormulaReferenceSelectionSurface");
                using (var gesture = new NativeFormulaGesture(pickerWindow, preview, Local(2.5, 1.5)))
                {
                    gesture.Move(preview, Local(2.5, 2.5)); gesture.Release(preview, Local(2.5, 2.5));
                }
                Check("picker-routed-range", picker.SelectedRange == new CellRange(new CellAddress(1, 2), new CellAddress(2, 2)));
                CaptureNative(pickerWindow, "formula-ux-cross-sheet.png");
                Check("picker-native-render", picker.PreviewRenderCount > 0);
                var apply = picker.GetVisualDescendants().OfType<Button>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == "nera-reference-apply");
                apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check("picker-apply-one-draft", left.IsEditing && ReferenceEquals(session.Editor.State, edit) && left.EditorText == "=SUM('O''Brien'!C2:C3" && !pickerWindow.IsVisible);
                var formula = left.EditorText + ")";
                left.UpdateEditorDraft(formula, formula.Length, formula.Length);
                Check("cross-sheet-formula-commit", left.CommitEditor() && first.GetCell(default).Formula == formula && first.GetCell(default).Value.ToString() == "30");
                Check("cross-sheet-formula-undo", session.Undo() && first.GetCell(default).IsEmpty && !session.Undo());
                left.BeginEdit("=SUM("); var draft = left.CurrentEditorDraft;
                ShowFormulaReferencePicker(other);
                var cancelWindow = _dialogs.Last(); var cancelPicker = (NeraFormulaReferencePicker)cancelWindow.Content!;
                cancelWindow.UpdateLayout();
                var cancel = cancelPicker.GetVisualDescendants().OfType<Button>()
                    .Single(button => AutomationProperties.GetAutomationId(button) == "nera-reference-cancel");
                cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check("picker-cancel-preserves-draft", left.CurrentEditorDraft == draft && left.IsEditing && !cancelWindow.IsVisible);
                left.CancelEditor();
                Console.WriteLine("NERA_AVALONIA_FORMULA_UX_SUCCESS " + JsonSerializer.Serialize(new
                {
                    sha, assertions = checks.Count, checks, nativeWindow = true, routedPointerInput = true,
                    productionFrameObserved = true, physicalInputTested = false,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    assembly = typeof(NeraSpreadsheetControl).Assembly.Location,
                }));
                lifetime.Shutdown(0);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("NERA_AVALONIA_FORMULA_UX_FAILURE " + exception + " completed=" + string.Join(',', checks));
                lifetime.Shutdown(1);
            }
        };
        _smokeTimer.Start();
    }

    private sealed class NativeFormulaGesture : IDisposable
    {
        private readonly Window _window;
        private readonly Control _source;
        private readonly Pointer _pointer = new(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        private bool _released;
        public NativeFormulaGesture(Window window, Control source, Point local)
        {
            _window = window; _source = source;
            var position = ToRoot(source, local);
            // Model implicit child capture so a transfer can expose capture-loss regressions.
            _pointer.Capture(source);
            source.RaiseEvent(new PointerPressedEventArgs(source, _pointer, window, position, 1,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        }
        public void Move(Control target, Point local)
        {
            var captured = _pointer.Captured as Control ?? _source;
            captured.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, captured, _pointer, _window, ToRoot(target, local), 2,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
        }
        public void Release(Control target, Point local)
        {
            var captured = _pointer.Captured as Control ?? _source;
            captured.RaiseEvent(new PointerReleasedEventArgs(captured, _pointer, _window, ToRoot(target, local), 3,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
            _released = true;
        }
        private Point ToRoot(Control control, Point point) => control.TranslatePoint(point, _window) ?? throw new InvalidOperationException("Native pointer target is detached.");
        public void Dispose()
        {
            if (!_released) _pointer.Capture(null);
            _pointer.Dispose();
        }
    }
}
