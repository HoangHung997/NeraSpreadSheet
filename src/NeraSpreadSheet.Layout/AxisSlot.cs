namespace NeraSpreadSheet.Layout;

public readonly record struct AxisSlot(int Index, double Start, double Size)
{
    public double End => Start + Size;
}
