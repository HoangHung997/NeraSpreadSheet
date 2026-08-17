namespace NeraSpreadSheet.Layout;

public readonly record struct AxisSlot(int Index, double Start, double Size, bool IsFrozen = false)
{
    public double End => Start + Size;
}
