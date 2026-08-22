namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetAutoFilterHost
{
    protected override void OnParentSet()
    {
        base.OnParentSet();
        ConfigurePointerRouting();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ConfigurePointerRouting();
    }

    private void ConfigurePointerRouting()
    {
        // The transparent overlay must not consume pan, pinch, tap or wheel
        // input intended for the production spreadsheet surface. Its visible
        // native filter-button children remain interactive.
        _buttonLayer.InputTransparent = true;
        _buttonLayer.CascadeInputTransparent = false;
    }
}
