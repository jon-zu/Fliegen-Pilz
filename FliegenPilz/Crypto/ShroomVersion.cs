namespace FliegenPilz.Crypto;

public struct ShroomVersion(ushort version)
{
    public ushort Version { get; } = version;

    public ShroomVersion Invert()
    {
        var v = -(short)Version - 1;
        return new ShroomVersion((ushort)v);
    }
}