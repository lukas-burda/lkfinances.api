namespace LkFinances.Core.Domain;

public static class Money
{
    public const string Currency = "BRL";

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
