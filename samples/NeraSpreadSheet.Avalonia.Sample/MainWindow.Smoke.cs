using System.Diagnostics;
using System.Text.Json;
using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class MainWindow
{
    private DispatcherTimer? _smokeTimer;

    internal void StartSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var deadline = Stopwatch.StartNew();
        _smokeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _smokeTimer.Tick += async (_, _) =>
        {
            if (_sheet.RenderedFrameCount == 0 && deadline.Elapsed < TimeSpan.FromSeconds(15)) return;
            _smokeTimer?.Stop();
            try
            {
                var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
                Require(sha is { Length: 40 } && sha.All(Uri.IsHexDigit), "An exact source SHA is required.");
                Require(_sheet.RenderedFrameCount > 0, "The native window did not render a workbook.");
                var framesBefore = _sheet.RenderedFrameCount;
                var workbook = new Workbook();
                var first = workbook.Worksheets[0];
                var second = workbook.AddWorksheet("Khác");
                first.SetValue(default, 3d);
                first.SetFormula(new CellAddress(0, 1), "=A1*2");
                var session = new SpreadsheetSession(workbook);
                session.Recalculate();
                _sheet.Session = session;
                Require(_sheet.BeginEdit("21"), "Cannot begin canonical edit.");
                Require(_sheet.CommitEditor(), "Cannot commit canonical edit.");
                Require(first.GetCell(new CellAddress(0, 1)).Value.ToString() == "42", "Dependent formula did not recalculate.");
                Require(session.Undo() && first.GetCell(default).Value.ToString() == "3", "Undo failed.");
                Require(session.Redo() && first.GetCell(default).Value.ToString() == "21", "Redo failed.");
                _sheet.BeginEdit("999");
                session.ActivateWorksheet(second);
                Require(!_sheet.IsEditing && !_sheet.CancelEditor(), "A stale native draft survived sheet activation.");
                Require(first.GetCell(default).Value.ToString() == "21" && second.GetCell(default).Value.ToString() == string.Empty,
                    "Cancelled draft changed worksheet data.");
                _sheet.Zoom = 1.25;
                _sheet.ScrollTo(10.25, 20.75);
                Require(_sheet.ScrollSnapshot.OffsetX == 10.25 && _sheet.ScrollSnapshot.OffsetY == 20.75,
                    "Continuous scroll offsets were quantized.");
                using var stream = new MemoryStream();
                await _serializer.SaveSessionAsync(session, stream, new OpenXmlExportOptions());
                stream.Position = 0;
                var loaded = await _serializer.LoadSessionAsync(stream, new OpenXmlImportOptions());
                Require(loaded.Workbook.Worksheets[0].GetCell(default).Value.ToString() == "21", "XLSX value round-trip failed.");
                Require(loaded.Workbook.Worksheets[0].GetCell(new CellAddress(0, 1)).Formula == "=A1*2", "XLSX formula round-trip failed.");
                _sheet.Session = loaded;
                loaded.ActivateWorksheet(loaded.Workbook.Worksheets[0]);
                _sheet.ScrollTo(0, 0);
                UpdateLayout();
                var output = Environment.GetEnvironmentVariable("NERA_AVALONIA_ARTIFACTS") ?? "artifacts/avalonia";
                Directory.CreateDirectory(output);
                using (var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(Bounds.Width),
                    (int)Math.Ceiling(Bounds.Height)), new Vector(96, 96)))
                {
                    bitmap.Render(this);
                    using var png = File.Create(Path.Combine(output, "native-window.png"));
                    bitmap.Save(png, new PngBitmapEncoderOptions());
                }
                Console.WriteLine("NERA_AVALONIA_SMOKE_SUCCESS " + JsonSerializer.Serialize(new
                {
                    sha, framesBefore, framesAfter = _sheet.RenderedFrameCount, assertions = 12,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    assembly = typeof(NeraSpreadsheetControl).Assembly.Location,
                    nativeWindow = true, physicalInputTested = false,
                }));
                lifetime.Shutdown(0);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("NERA_AVALONIA_SMOKE_FAILURE " + exception);
                lifetime.Shutdown(1);
            }
        };
        _smokeTimer.Start();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
