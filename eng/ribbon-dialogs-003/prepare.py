"""One-shot, exact-baseline source preparation. Never updates a branch itself."""
import json
import os
from pathlib import Path
import subprocess
import sys
import urllib.request
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape, quoteattr

ROOT=Path.cwd()
REPO='HoangHung997/NeraSpreadSheet'
BRANCH='feature/ribbon-dialogs-003'
BASE='66730639278f4304cf0493aa85c0c499df3e8ab7'
SELF='eng/ribbon-dialogs-003/prepare.py'
WORKFLOW='.github/workflows/ribbon-dialogs-003-prepare.yml'

def replace(path,old,new,count=1):
    target=ROOT/path; text=target.read_text(encoding='utf-8-sig')
    actual=text.count(old)
    if actual != count: raise ValueError(f'Patch drift {path}: expected {count}, found {actual}: {old[:80]!r}')
    target.write_text(text.replace(old,new),encoding='utf-8',newline='\n')

def prepare():
    subprocess.run(['git','merge-base','--is-ancestor',BASE,'HEAD'],check=True)
    replace('src/NeraSpreadSheet.Ribbon.Core/RibbonItemDefinition.cs',
        '    public RibbonItemKind Kind { get; private init; } = RibbonItemKind.Button;',
        '''    /// <summary>Places this ordinary registered command in the group caption footer.
    /// Customization, overflow and keyboard routing retain its normal command identity.</summary>
    public bool IsDialogLauncher { get; private init; }

    public static RibbonItemDefinition DialogLauncher(CommandId commandId, int order = int.MaxValue) =>
        new(commandId, RibbonItemKind.Button, order: order) { IsDialogLauncher = true };

    public RibbonItemKind Kind { get; private init; } = RibbonItemKind.Button;''')
    replace('src/NeraSpreadSheet.Ribbon.Core/RibbonItemDefinition.cs',
        'this with { IsLarge = isLarge, Order = order }', 'this with { IsLarge = !IsDialogLauncher && isLarge, Order = order }')
    path='src/NeraSpreadSheet.Ribbon.Core/RibbonResponsiveLayout.cs'
    replace(path,'if (Sizes[index] < targetSize)','if (!Presentation.Items[index].Definition.IsDialogLauncher && Sizes[index] < targetSize)')
    replace(path,'            var items = Presentation.Items.Select((item, index) =>',
        '            var captionHeight = Presentation.Items.Any(item => item.Definition.IsDialogLauncher) ? Math.Max(18d, metrics.GroupCaptionHeight) : metrics.GroupCaptionHeight;\n            var items = Presentation.Items.Select((item, index) =>')
    replace(path,'''                    Y = (metrics.GroupPadding + _packing.Placements[index].Row *
                        (metrics.RowHeight + metrics.RowSpacing)) * scale,
                    Height = (_packing.Placements[index].RowSpan == metrics.RowCount
                        ? contentHeight
                        : metrics.RowHeight) * scale,''',
        '''                    Y = (item.Definition.IsDialogLauncher ? metrics.GroupPadding + contentHeight :
                        metrics.GroupPadding + _packing.Placements[index].Row * (metrics.RowHeight + metrics.RowSpacing)) * scale,
                    Height = (item.Definition.IsDialogLauncher ? captionHeight :
                        _packing.Placements[index].RowSpan == metrics.RowCount ? contentHeight : metrics.RowHeight) * scale,''')
    replace(path,'Height = (2d * metrics.GroupPadding + contentHeight + metrics.GroupCaptionHeight) * scale,','Height = (2d * metrics.GroupPadding + contentHeight + captionHeight) * scale,')
    replace(path,'CaptionHeight = metrics.GroupCaptionHeight * scale,','CaptionHeight = captionHeight * scale,')
    replace(path,'            Presentation.Items[index].Kind != RibbonItemKind.Separator &&',
        '            !Presentation.Items[index].Definition.IsDialogLauncher && Presentation.Items[index].Kind != RibbonItemKind.Separator &&')
    replace(path,'            var x = metrics.GroupPadding;\n            for (var index = 0; index < sizes.Length; index++)',
        '''            var x = metrics.GroupPadding;
            var launchers = new List<int>();
            var launcherSize = Math.Max(18d, metrics.GroupCaptionHeight);
            for (var index = 0; index < sizes.Length; index++)''')
    replace(path,'                var span = sizes[index] == RibbonItemSize.Large ||',
        '                if (Presentation.Items[index].Definition.IsDialogLauncher) { launchers.Add(index); continue; }\n                var span = sizes[index] == RibbonItemSize.Large ||')
    replace(path,'var commandWidth = sizes.Length == 0 ? 0d : x - metrics.GroupPadding - metrics.Spacing;',
        'var commandWidth = sizes.Length == launchers.Count ? 0d : x - metrics.GroupPadding - metrics.Spacing;')
    replace(path,'var widthWithCaption = Math.Max(commandWidth, MeasureCaption(Presentation.Caption));',
        'var widthWithCaption = Math.Max(commandWidth, MeasureCaption(Presentation.Caption) + launchers.Count * (launcherSize + 2d));')
    replace(path,'            return new Packing(placements, groupWidth);',
        '''            for (var index = 0; index < launchers.Count; index++)
                placements[launchers[index]] = new Placement(groupWidth - metrics.GroupPadding -
                    (launchers.Count - index) * (launcherSize + 2d) + 2d, launcherSize, metrics.RowCount, 1, -1);
            return new Packing(placements, groupWidth);''')
    replace(path,'                    placements[itemIndex] = placements[itemIndex] with { Width = columnWidth };',
        '                    if (!Presentation.Items[itemIndex].Definition.IsDialogLauncher)\n                        placements[itemIndex] = placements[itemIndex] with { Width = columnWidth };')
    replace('src/NeraSpreadSheet.Avalonia/NeraRibbonControl.Items.cs',
        'private Control BuildItem(RibbonItemLayout item) => item.Presentation.Kind switch',
        'private Control BuildItem(RibbonItemLayout item) => item.Presentation.Definition.IsDialogLauncher ? BuildDialogLauncher(item) : item.Presentation.Kind switch')
    path='src/NeraSpreadSheet.Avalonia/NeraRibbonControl.cs'
    replace(path,'Width = canvas.Width, Height = group.CaptionHeight / scale',
        'Width = group.Items.Any(item => item.Presentation.Definition.IsDialogLauncher) ? Math.Max(0, group.Items.Where(item => item.Presentation.Definition.IsDialogLauncher).Min(item => item.X) / scale - 2) : canvas.Width, Height = group.CaptionHeight / scale')
    replace(path,'            Canvas.SetTop(caption, group.CaptionY / scale);',
        '            SetIdentity(caption, "ribbon-group-caption-" + group.Presentation.Id, group.Presentation.Caption);\n            Canvas.SetTop(caption, group.CaptionY / scale);')
    path='src/NeraSpreadSheet.Avalonia/NeraSpreadsheetRibbonPreset.cs'
    text=(ROOT/path).read_text(); start=text.index('        static RibbonGroupDefinition Group('); end=text.index('\n        void AddTab(', start)
    old=text[start:end]
    new='''        RibbonGroupDefinition Group(string id, string caption, int priority, params RibbonItemDefinition?[] items)
        {
            var result = items.OfType<RibbonItemDefinition>().ToList();
            var launcher = id switch
            {
                "number" => "Ui.Dialog.Number", "font" => "Ui.Dialog.Font", "alignment" => "Ui.Dialog.Alignment",
                "page-setup" => "Ui.Dialog.PageSetup", "print-options" => "Ui.Dialog.PrintOptions", "zoom" => "Ui.Dialog.Zoom",
                _ => null,
            };
            if (launcher is not null && Available(new CommandId(launcher))) result.Add(RibbonItemDefinition.DialogLauncher(launcher));
            return new RibbonGroupDefinition(id, caption, result, 0, priority) { CaptionResourceKey = caption };
        }
'''
    replace(path,old,new)
    path='src/NeraSpreadSheet.Editing/SpreadsheetStyleController.cs'
    replace(path,'''    public void ApplyToSelection(
        Func<CellStyle, CellStyle> transform,
        string description)
    {''', '''    /// <summary>Applies explicit properties, including values already equal to the
    /// active cell. Whole-axis mutations must not infer this intent from one cell.</summary>
    public void ApplyPatchToSelection(CellStylePatch patch, string description)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!patch.IsEmpty) ApplyToSelectionCore(patch.Apply, description, patch);
    }

    public void ApplyToSelection(Func<CellStyle, CellStyle> transform, string description) =>
        ApplyToSelectionCore(transform, description, null);

    private void ApplyToSelectionCore(
        Func<CellStyle, CellStyle> transform, string description, CellStylePatch? explicitPatch)
    {''')
    replace(path,'var axisPatch = CellStylePatch.FromDifference(', 'var axisPatch = explicitPatch ?? CellStylePatch.FromDifference(')
    path='samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.Ribbon.cs'
    marker='    private static string ColorKey'
    text=(ROOT/path).read_text(); end=text.index(marker); before=text[:end]; pos=before.rfind('\n    }')
    # The last method before ColorKey must be RegisterCommands, never another method.
    if 'Choice("Ui.Theme"' not in before[pos-1200:pos]: raise ValueError('RegisterCommands boundary drift')
    before=before[:pos]+'\n        RegisterDialogCommands();'+before[pos:]
    (ROOT/path).write_text(before+text[end:],encoding='utf-8')
    path='samples/NeraSpreadSheet.Avalonia.Sample/Program.cs'
    replace(path,'                if (desktop.Args?.Contains("--ribbon-visual-smoke", StringComparer.Ordinal) == true)',
        '''                if (desktop.Args?.Contains("--dialogs-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => StartRibbonAfterNativeFrame(window, desktop, true);
                else if (desktop.Args?.Contains("--ribbon-visual-smoke", StringComparer.Ordinal) == true)''')
    replace(path,'private static void StartRibbonAfterNativeFrame(FullShellWindow window, IClassicDesktopStyleApplicationLifetime lifetime)',
        'private static void StartRibbonAfterNativeFrame(FullShellWindow window, IClassicDesktopStyleApplicationLifetime lifetime, bool dialogs = false)')
    replace(path,'            window.StartRibbonVisualSmoke(lifetime);',
        '            if (dialogs) window.StartDialogsSmoke(lifetime); else window.StartRibbonVisualSmoke(lifetime);')
    replace('samples/NeraSpreadSheet.Avalonia.Sample/FullShellWindow.RibbonVisual.cs',
        '                Require(origin.Y + control.Bounds.Height <= group.CaptionY / snapshot.Scale + 0.6, "caption area intrusion " + id);',
        '''                if (item.Presentation.Definition.IsDialogLauncher)
                    Require(Math.Abs(origin.Y - group.CaptionY / snapshot.Scale) <= 0.6 &&
                        origin.X + control.Bounds.Width <= nativeGroup.Bounds.Width + 0.6 &&
                        control.Bounds.Width >= 18 && control.Bounds.Height >= 18, "caption launcher geometry " + id);
                else Require(origin.Y + control.Bounds.Height <= group.CaptionY / snapshot.Scale + 0.6, "caption area intrusion " + id);''')
    # Native TextChanged may be deferred; pending intent uses immediate property changes.
    path='src/NeraSpreadSheet.Avalonia/NeraFormatCellsDialog.cs'
    replace(path,'    private readonly TabControl _tabs = new();','    private readonly TabControl _tabs = new();\n    private string? _numberParameterError;')
    replace(path,'        var patch = new CellStylePatch();','        if (_numberParameterError is not null) throw new ArgumentException(_numberParameterError);\n        var patch = new CellStylePatch();')
    replace(path,'''        if (control is TextBox text) text.TextChanged += (_, _) => _dirty.Add(key);
        else if (control is ComboBox choice) choice.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, choice)) _dirty.Add(key); };
        else if (control is CheckBox check) check.IsCheckedChanged += (_, _) => _dirty.Add(key);''',
        '''        void Mark(bool changed) { if (changed) _dirty.Add(key); else _dirty.Remove(key); }
        if (control is TextBox text) { var initial = text.Text; text.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Mark(text.Text != initial); }; }
        else if (control is ComboBox choice) { var initial = Selected(choice); choice.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, choice)) Mark(Selected(choice) != initial); }; }
        else if (control is CheckBox check) { var initial = check.IsChecked; check.IsCheckedChanged += (_, _) => Mark(check.IsChecked != initial); }''')
    replace(path,'ArgumentException.ThrowIfNullOrWhiteSpace(family.Text); return patch with { FontFamily = family.Text.Trim() };',
        'var value = family.Text; ArgumentException.ThrowIfNullOrWhiteSpace(value); return patch with { FontFamily = value.Trim() };')
    replace(path,'''            if (!Enum.TryParse<SpreadsheetNumberFormatCategory>(Selected(category), out var selected) || selected == SpreadsheetNumberFormatCategory.Custom) return;
            try { code.Text = SpreadsheetNumberFormats.Create(selected, ReadInteger(decimals, 0, 15), thousands.IsChecked == true, currency.Text ?? string.Empty, parentheses.IsChecked == true); }
            catch (ArgumentException) { preview.Text = L("Thông số tạo định dạng chưa hợp lệ."); }''',
        '''            _numberParameterError = null;
            if (!Enum.TryParse<SpreadsheetNumberFormatCategory>(Selected(category), out var selected) || selected == SpreadsheetNumberFormatCategory.Custom) return;
            try
            {
                var hasDecimals = selected is SpreadsheetNumberFormatCategory.Number or SpreadsheetNumberFormatCategory.Currency or SpreadsheetNumberFormatCategory.Accounting or SpreadsheetNumberFormatCategory.Percentage or SpreadsheetNumberFormatCategory.Scientific;
                code.Text = SpreadsheetNumberFormats.Create(selected, hasDecimals ? ReadInteger(decimals, 0, 15) : 2, thousands.IsChecked == true, currency.Text ?? string.Empty, parentheses.IsChecked == true);
            }
            catch (ArgumentException) { _numberParameterError = L("Thông số tạo định dạng chưa hợp lệ."); preview.Text = _numberParameterError; }''')
    replace(path,'        decimals.TextChanged += (_, _) => Generate(); currency.TextChanged += (_, _) => Generate();',
        '        decimals.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Generate(); }; currency.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Generate(); };')
    replace(path,'        code.TextChanged += (_, _) => Preview(); Preview();',
        '        code.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Preview(); }; Preview();')
    replace('src/NeraSpreadSheet.Avalonia/NeraPageSetupDialog.cs',
        'text.TextChanged += (_, _) => Mark(text.Text != initial);',
        'text.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Mark(text.Text != initial); };')
    replace('src/NeraSpreadSheet.Avalonia/NeraSettingsDialog.cs','protected readonly DockPanel DialogBody = new()', 'protected DockPanel DialogBody { get; } = new()')
    replace('src/NeraSpreadSheet.Editing/SpreadsheetNumberFormats.cs','using System.Text;\n\n','')
    # One canonical qualification workflow: old consolidation branch stays archived.
    path='.github/workflows/check-out.yml'
    replace(path,'branches: [main, feature/consolidate-checkout-001]','branches: [main, feature/ribbon-dialogs-003]')
    replace(path,"@('smoke', 'full-ui-smoke', 'formula-ux-smoke', 'ribbon-visual-smoke')", "@('smoke', 'full-ui-smoke', 'formula-ux-smoke', 'ribbon-visual-smoke', 'dialogs-smoke')")
    replace(path,"if ($LASTEXITCODE -ne 0) { throw 'Published Ribbon evidence validation failed.' }", "if ($LASTEXITCODE -ne 0) { throw 'Published Ribbon evidence validation failed.' }\n          python scripts/verify-avalonia-dialogs.py artifacts/published-images/dialogs $env:NERA_SOURCE_SHA\n          if ($LASTEXITCODE -ne 0) { throw 'Published dialog evidence validation failed.' }")
    path='.github/workflows/avalonia-primary-ribbon-002.yml'
    replace(path,"@{ Argument = '--ribbon-visual-smoke'; Prefix = 'NERA_AVALONIA_RIBBON_VISUAL_SUCCESS '; Minimum = 190 }", "@{ Argument = '--ribbon-visual-smoke'; Prefix = 'NERA_AVALONIA_RIBBON_VISUAL_SUCCESS '; Minimum = 190 },\n            @{ Argument = '--dialogs-smoke'; Prefix = 'NERA_AVALONIA_DIALOGS_SUCCESS '; Minimum = 70 }")
    replace(path,"if ($LASTEXITCODE -ne 0) { throw 'Ribbon visual matrix or image provenance failed.' }", "if ($LASTEXITCODE -ne 0) { throw 'Ribbon visual matrix or image provenance failed.' }\n          python scripts/verify-avalonia-dialogs.py artifacts/avalonia/dialogs $env:NERA_SOURCE_SHA\n          if ($LASTEXITCODE -ne 0) { throw 'Dialog image provenance failed.' }")
    path='scripts/check-out/pack.py'
    replace(path,"'ribbon-visual-smoke': ('NERA_AVALONIA_RIBBON_VISUAL_SUCCESS ', 190),", "'ribbon-visual-smoke': ('NERA_AVALONIA_RIBBON_VISUAL_SUCCESS ', 190),\n        'dialogs-smoke': ('NERA_AVALONIA_DIALOGS_SUCCESS ', 70),")
    images={'format-number.png':'number','format-font.png':'font','format-alignment.png':'alignment','format-border.png':'border','format-fill.png':'fill','page-setup.png':'page','page-margins.png':'margins','page-sheet.png':'sheet','zoom-dialog.png':'zoom'}
    replace(path,"    shutil.rmtree(output / 'staging')", "    for target, source in "+repr(images)+".items():\n        shutil.copy2(images / 'dialogs' / ('Light-' + source + '.png'), output / target)\n    shutil.rmtree(output / 'staging')")
    path='scripts/check-out/assemble.py'
    replace(path,"('ribbon-light.png','ribbon-dark.png','full-window.png','customization.png')",repr(tuple(['ribbon-light.png','ribbon-dark.png','full-window.png','customization.png']+list(images))))
    path='scripts/check-out/test_delivery.py'
    replace(path,"'ribbon-visual-smoke':('NERA_AVALONIA_RIBBON_VISUAL_SUCCESS ',198)}", "'ribbon-visual-smoke':('NERA_AVALONIA_RIBBON_VISUAL_SUCCESS ',198),\n            'dialogs-smoke':('NERA_AVALONIA_DIALOGS_SUCCESS ',80)}")
    replace(path,"        self.prefixes={", "        dialogs=self.images/'dialogs';dialogs.mkdir()\n        for name in "+repr(tuple(images.values()))+":\n            (dialogs/('Light-'+name+'.png')).write_bytes(b'synthetic delivery fixture, not native screenshot')\n        self.prefixes={")
    replace(path,'    def testFrameworkDependentAppCannotBeCalledSelfContained(self):', '''    def testMissingDialogSmokeCannotProduceDownload(self):
        (self.images/'dialogs-smoke.log').unlink()
        with self.assertRaises(FileNotFoundError):self.pack()

    def testWrongDialogHeadCannotProduceDownload(self):
        prefix,count=self.prefixes['dialogs-smoke']
        (self.images/'dialogs-smoke.log').write_text(prefix+json.dumps(dict(sha=OLD,nativeWindow=True,assertions=count)))
        with self.assertRaisesRegex(ValueError,'evidence'):self.pack()

    def testFrameworkDependentAppCannotBeCalledSelfContained(self):''')
    path='Check out/README.md'
    replace(path,'## Sau khi giải nén', '''## Hộp thoại cài đặt nhóm Ribbon

Ở **Trang đầu**, nhấn mũi tên nhỏ góc dưới bên phải nhóm **Số**, **Phông chữ** hoặc **Căn chỉnh** để mở đúng thẻ trong **Định dạng ô**. `Ctrl+1` mở Định dạng ô. Có thêm thẻ Đường viền/Màu nền. Ở **Bố trí trang** có Thiết lập trang/Tùy chọn in; ở **Xem** có Thu phóng.

![Định dạng số](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-number.png)
![Phông chữ](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-font.png)
![Căn chỉnh](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/format-alignment.png)
![Thiết lập trang](https://github.com/HoangHung997/NeraSpreadSheet/releases/download/check-out-latest/page-setup.png)

OK áp dụng các thuộc tính đã chỉnh qua Undo/Redo; Cancel/Esc không áp dụng. Dialog đọc vùng hiện hành; vùng lớn không bị materialize để lấy thông tin. Đây là tập dialog đầu tiên, không phải toàn bộ Format Cells/Print/Protection của Excel. Xem [phạm vi và giới hạn](../docs/worklog/RIBBON_DIALOGS_003.md).

## Sau khi giải nén''')
    resources()
    print('PREPARED_DIALOG_SOURCE: guarded replacements applied; no branch moved')

def resources():
    translations={
        'Định dạng ô':'Format Cells','Không thể áp dụng':'Cannot apply','Đồng ý':'OK','Hủy':'Cancel','Giữ nguyên / nhiều giá trị':'Unchanged / mixed values',
        'Giá trị phải nằm trong khoảng {0} đến {1}.':'Enter a value between {0} and {1}.','Cần nhập số nguyên.':'Enter a whole number.',
        'Số':'Number','Phông chữ':'Font','Căn chỉnh':'Alignment','Đường viền':'Border','Màu nền':'Fill','Loại định dạng':'Category',
        'Số chữ số thập phân':'Decimal places','Phân cách hàng nghìn':'Thousands separator','Ký hiệu tiền tệ':'Currency symbol','Số âm trong ngoặc':'Negative numbers in parentheses',
        'Mã định dạng':'Format code','Mẫu hiển thị':'Sample','Mã định dạng chưa hợp lệ.':'The format code is not valid.','Thông số tạo định dạng chưa hợp lệ.':'The format parameters are not valid.',
        'Cỡ chữ':'Font size','Đậm':'Bold','Nghiêng':'Italic','Gạch ngang':'Strikethrough','Gạch chân':'Underline','Không':'None','Đơn':'Single','Kép':'Double',
        'Chỉ số':'Position','Bình thường':'Normal','Chỉ số trên':'Superscript','Chỉ số dưới':'Subscript','Màu chữ (#RRGGBB)':'Font color (#RRGGBB)',
        'Căn ngang':'Horizontal alignment','Căn dọc':'Vertical alignment','Ngắt dòng':'Wrap text','Thu nhỏ vừa ô':'Shrink to fit','Thụt lề':'Indent','Góc xoay chữ':'Text rotation',
        'Mẫu đường viền':'Border preset','Không viền':'No borders','Tất cả đường viền':'All borders','Viền dưới':'Bottom border','Kiểu nét':'Line style',
        'Mảnh':'Thin','Vừa':'Medium','Dày':'Thick','Nét đứt':'Dashed','Nét chấm':'Dotted','Nét đôi':'Double','Màu viền (#RRGGBB)':'Border color (#RRGGBB)',
        'Dùng màu nền':'Use fill color','Màu nền (#RRGGBB)':'Fill color (#RRGGBB)','Chung':'General','Tiền tệ':'Currency','Kế toán':'Accounting','Ngày tháng':'Date',
        'Thời gian':'Time','Phần trăm':'Percentage','Phân số':'Fraction','Khoa học':'Scientific','Văn bản':'Text','Tùy chỉnh':'Custom',
        'Căn trái':'Left','Căn giữa':'Center','Căn phải':'Right','Lấp đầy':'Fill','Căn đều':'Justify','Giữa vùng chọn':'Center across selection','Phân bố đều':'Distributed','Trên':'Top','Giữa':'Center','Dưới':'Bottom',
        'Thiết lập trang':'Page Setup','Trang':'Page','Lề trang':'Margins','Trang tính':'Sheet','Hướng giấy':'Orientation','Dọc':'Portrait','Ngang':'Landscape','Khổ giấy':'Paper size','Khổ giấy từ file':'Imported paper size',
        'Chế độ co giãn':'Scaling mode','Tỷ lệ phần trăm':'Percentage','Vừa số trang':'Fit to pages','Tỷ lệ in (%)':'Print scale (%)','Số trang theo chiều rộng':'Pages wide','Số trang theo chiều cao':'Pages tall',
        'Lề trái (mm)':'Left margin (mm)','Lề phải (mm)':'Right margin (mm)','Lề trên (mm)':'Top margin (mm)','Lề dưới (mm)':'Bottom margin (mm)','Đầu trang (mm)':'Header margin (mm)','Cuối trang (mm)':'Footer margin (mm)',
        'Giữa trang theo chiều ngang':'Center horizontally','Giữa trang theo chiều dọc':'Center vertically','In đường lưới':'Print gridlines','In tiêu đề hàng cột':'Print row and column headings',
        'Nội dung đầu trang':'Header content','Nội dung cuối trang':'Footer content','Thu phóng':'Zoom','Thu phóng (%)':'Zoom (%)',
        'Nhập ít nhất một chiều số trang.':'Enter at least one fit-to-page dimension.',
        'Vùng chọn hoặc trang tính đã thay đổi. Hãy mở lại hộp thoại.':'The selection or worksheet changed. Reopen the dialog.',
        'Ô trống hoặc ô có nhiều định dạng: chỉ thuộc tính bạn chỉnh sẽ thay đổi.':'Blank or mixed fields are unchanged unless you edit them.',
        'Vùng lớn: không quét toàn bộ ô. Chỉ thuộc tính bạn chỉnh sẽ thay đổi.':'Large selection: common values are not scanned. Only edited properties will change.',
        'Mẫu dùng bộ định dạng hiện tại; mã tùy chỉnh và căn khoảng trắng kế toán chưa đảm bảo giống Excel hoàn toàn. Giá trị và công thức gốc không đổi.':'The sample uses the current formatter. Custom directives and accounting spacing are not guaranteed to match Excel. Raw values and formulas are unchanged.',
        'Phông chữ cần có trên máy chạy. Các thuộc tính không chỉnh được giữ nguyên.':'Fonts must be available on the running device. Unedited properties are preserved.',
        'Gộp ô là lệnh riêng trên Ribbon; không tự gộp hoặc xóa dữ liệu khi căn chỉnh.':'Merge is a separate Ribbon command. Alignment does not merge cells or remove data.',
        'Đường viền áp dụng cho từng ô. Chọn một mẫu để thay toàn bộ đường viền; không chọn thì giữ nguyên.':'Borders apply per cell. Choose a preset to replace all border sides, or leave it unselected to preserve them.',
        'Mẫu tô khác trong file được giữ khi không chỉnh; thay màu nền sẽ dùng màu đặc.':'Imported fill patterns are preserved when untouched. Changing fill uses a solid color.',
        'Để trống số trang để tự động. Tỷ lệ in không thay đổi mức thu phóng trên màn hình.':'Leave page counts blank for automatic. Print scaling does not change screen zoom.',
        'Vùng in, tiêu đề lặp và ngắt trang từ file được giữ nguyên. Chưa có trình soạn đầu/cuối trang đầy đủ như Excel.':'Imported print areas, repeat titles and manual breaks are preserved. A complete Excel header/footer designer is not implemented.',
        'Chỉ thay đổi chế độ xem, không thay đổi dữ liệu hoặc tỷ lệ in.':'Changes the view only, not cell data or print scaling.',
        'Định dạng ô: Số':'Format Cells: Number','Định dạng ô: Phông chữ':'Format Cells: Font','Định dạng ô: Căn chỉnh':'Format Cells: Alignment',
        'Thiết lập trang nâng cao':'Advanced Page Setup','Tùy chọn trang tính khi in':'Sheet printing options','Thiết lập thu phóng':'Zoom settings',
    }
    for filename,english in [('PresentationStrings.resx',False),('PresentationStrings.en.resx',True)]:
        path=ROOT/'src/NeraSpreadSheet.Commands'/filename
        text=path.read_text(encoding='utf-8-sig'); parsed=ET.fromstring(text); keys={entry.attrib['name'] for entry in parsed.findall('data')}
        additions=''.join('  <data name='+quoteattr(key)+' xml:space="preserve"><value>'+escape(value if english else key)+'</value></data>\n' for key,value in translations.items() if key not in keys)
        if text.count('</root>') != 1: raise ValueError('Resource structure drift')
        path.write_text(text.replace('</root>',additions+'</root>'),encoding='utf-8')

def materialize():
    if os.environ.get('GITHUB_REPOSITORY') != REPO or os.environ.get('GITHUB_REF_NAME') != BRANCH: raise ValueError('Wrong preparation context')
    parent=os.environ['GITHUB_SHA']; base_tree=subprocess.check_output(['git','rev-parse','HEAD^{tree}'],text=True).strip()
    for path in (SELF,WORKFLOW): (ROOT/path).unlink()
    names=subprocess.check_output(['git','diff','--name-only','HEAD','--'],text=True).splitlines()
    entries=[]
    for name in names:
        path=ROOT/name; entry=dict(path=name,mode='100644',type='blob')
        if path.exists(): entry['content']=path.read_text(encoding='utf-8')
        else: entry['sha']=None
        entries.append(entry)
    def api(endpoint,body):
        request=urllib.request.Request('https://api.github.com/repos/'+REPO+'/'+endpoint,data=json.dumps(body).encode(),headers={'Authorization':'Bearer '+os.environ['GH_TOKEN'],'Accept':'application/vnd.github+json','Content-Type':'application/json'},method='POST')
        with urllib.request.urlopen(request,timeout=60) as response:return json.load(response)
    tree=api('git/trees',dict(base_tree=base_tree,tree=entries))['sha']
    commit=api('git/commits',dict(message='feat(ribbon): add group dialog launchers and transactional Avalonia settings dialogs',tree=tree,parents=[parent]))['sha']
    receipt=dict(sourceSha=commit,treeSha=tree,parentSha=parent,changedPaths=names,refUpdated=False)
    Path('artifacts/preparation').mkdir(parents=True,exist_ok=True)
    Path('artifacts/preparation/receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('DIALOG_SOURCE_PREPARED '+json.dumps(receipt))

if __name__=='__main__':
    if len(sys.argv)>1 and sys.argv[1]=='--materialize':materialize()
    else:prepare()
