namespace NeraSpreadSheet.Commands;

public readonly record struct CommandId
{
    public CommandId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A command id is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator CommandId(string value) => new(value);
}
