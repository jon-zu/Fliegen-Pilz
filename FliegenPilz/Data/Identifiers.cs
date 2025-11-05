namespace FliegenPilz.Data;

public readonly record struct AccountId(int Value)
{
    public override string ToString() => Value.ToString();
    public static implicit operator int(AccountId id) => id.Value;
    public static implicit operator AccountId(int value) => new(value);
}

public readonly record struct CharacterId(int Value)
{
    public override string ToString() => Value.ToString();
    public static implicit operator int(CharacterId id) => id.Value;
    public static implicit operator CharacterId(int value) => new(value);
}
