namespace FliegenPilz.World;

public readonly record struct WorldId(int Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct ChannelId(int Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct MapId(int Value)
{
    public override string ToString() => Value.ToString();
}

public readonly record struct RoomId(int InstanceId, MapId FieldId)
{
    public override string ToString() => $"{FieldId.Value}:{InstanceId}";
}
