using System.Runtime.ExceptionServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using WinFormsApplication = System.Windows.Forms.Application;
using WinFormsBackend = NeraSpreadSheet.WinForms.WinFormsRenderingBackend;
using WinFormsControl = NeraSpreadSheet.WinForms.NeraSpreadsheetControl;
using WinFormsForm = System.Windows.Forms.Form;
using WpfControl = NeraSpreadSheet.Wpf.NeraSpreadsheetControl;
using WpfWindow = System.Windows.Window;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TableStyleDesktopRendererSmokeTests
{
    private static readonly TimeSpan StaTimeout = TimeSpan.FromSeconds(60d);
    private static readonly ColorRgba HeaderColor = new(36, 92, 154);

    [TestMethod]
    [Timeout(90_000)]
    public void WpfDrawingContextShouldRenderSharedTableStyleInLoadedControl()
    {
        RunInSta(() =>
        {
            using var control = new WpfControl
            {
                Workbook = CreateWorkbook(),
                Width = 240d,
                Height = 100d,
                RenderTheme = new SpreadsheetRenderTheme { ShowHeaders = false },
            };
            var window = new WpfWindow
            {
                Content = control,
                Width = 240d,
                Height = 100d,
                Left = -32_000d,
                Top = -32_000d,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap(
                    240,
                    100,
                    96d,
                    96d,
                    PixelFormats.Pbgra32);
                bitmap.Render(control);
                var pixels = new byte[240 * 100 * 4];
                bitmap.CopyPixels(pixels, 240 * 4, 0);
                AssertPixel(pixels, 240, 20, 10, HeaderColor);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [TestMethod]
    [Timeout(90_000)]
    public void WinFormsAndDirect2DShouldRenderSameSharedTableStyleInLoadedControl()
    {
        RunInSta(() =>
        {
            using var form = new WinFormsForm
            {
                ClientSize = new System.Drawing.Size(240, 100),
                Location = new System.Drawing.Point(-32_000, -32_000),
                ShowInTaskbar = false,
            };
            using var control = new WinFormsControl
            {
                Workbook = CreateWorkbook(),
                Dock = System.Windows.Forms.DockStyle.Fill,
                RenderTheme = new SpreadsheetRenderTheme { ShowHeaders = false },
                RenderingBackend = WinFormsBackend.GdiPlus,
            };
            form.Controls.Add(control);
            form.Show();
            WinFormsApplication.DoEvents();
            using (var bitmap = new System.Drawing.Bitmap(240, 100))
            {
                control.DrawToBitmap(
                    bitmap,
                    new System.Drawing.Rectangle(0, 0, 240, 100));
                var actual = bitmap.GetPixel(20, 10);
                Assert.AreEqual(HeaderColor.Red, actual.R);
                Assert.AreEqual(HeaderColor.Green, actual.G);
                Assert.AreEqual(HeaderColor.Blue, actual.B);
            }

            control.RenderingBackend = WinFormsBackend.Direct2D;
            control.Invalidate();
            control.Update();
            WinFormsApplication.DoEvents();
            var diagnostics = control.Direct2DDiagnostics
                ?? throw new AssertFailedException(
                    "The Direct2D renderer was not created.");
            Assert.IsTrue(diagnostics.PixelWidth > 0);
            Assert.IsTrue(diagnostics.PixelHeight > 0);
        });
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var definition = new TableStyleDefinition(
            "custom:desktop-smoke",
            "DesktopSmoke",
            [
                new TableStyleElement(
                    TableStyleElementType.WholeTable,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromRgb(new ColorRgba(225, 238, 249)),
                    }),
                new TableStyleElement(
                    TableStyleElementType.HeaderRow,
                    new TableStyleFormat
                    {
                        FillColor = TableStyleColor.FromRgb(HeaderColor),
                        FontColor = TableStyleColor.FromRgb(ColorRgba.White),
                        FontWeight = 700,
                    }),
            ]);
        workbook.TableStyles.AddOrReplaceCustom(definition);
        workbook.Worksheets[0].AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "A"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "B"),
            ],
            styleName: definition.Name,
            showRowStripes: false));
        return workbook;
    }

    private static void AssertPixel(
        byte[] pixels,
        int width,
        int x,
        int y,
        ColorRgba expected)
    {
        var offset = ((y * width) + x) * 4;
        Assert.AreEqual(expected.Blue, pixels[offset]);
        Assert.AreEqual(expected.Green, pixels[offset + 1]);
        Assert.AreEqual(expected.Red, pixels[offset + 2]);
        Assert.AreEqual(expected.Alpha, pixels[offset + 3]);
    }

    private static void RunInSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "Nera Table style desktop smoke",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(StaTimeout))
        {
            Assert.Fail("The Table style desktop smoke did not complete.");
        }
        failure?.Throw();
    }
}
