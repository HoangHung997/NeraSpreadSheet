using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

public sealed record RibbonItemDefinition(
    CommandId CommandId,
    bool IsLarge = false,
    int Order = 0);
