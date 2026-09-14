namespace NeraSpreadSheet.Editing;

/// <summary>
/// Host-neutral native clipboard format names. Platform adapters store the text payload
/// in their standard Unicode text format and use <see cref="PayloadId"/> only to retain
/// the in-process rich package when the operating-system clipboard still represents the
/// same Nera copy/cut operation.
/// </summary>
public static class SpreadsheetClipboardNativeFormats
{
    public const string PayloadId = "NeraSpreadSheet.Clipboard.PayloadId.v1";
}
