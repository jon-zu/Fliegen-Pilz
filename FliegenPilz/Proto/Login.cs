using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FliegenPilz.Net;

namespace FliegenPilz.Proto;

public static class UnsafePacketPod<T> where T : unmanaged
{
    private static readonly int Size = Unsafe.SizeOf<T>();

    public static T FromSpan(ReadOnlySpan<byte> span)
    {
        if (span.Length != Size)
            throw new ArgumentException($"Span must be exactly {Size} bytes", nameof(span));

        T result = default;
        ref var dest = ref Unsafe.As<T, byte>(ref result);
        span.CopyTo(MemoryMarshal.CreateSpan(ref dest, Size));
        return result;
    }

    public static T FromSequence(ReadOnlySequence<byte> seq)
    {
        if (seq.Length != Size)
            throw new InvalidOperationException($"Invalid data length for {typeof(T).Name}.");

        Span<byte> buffer = stackalloc byte[Size];
        seq.CopyTo(buffer);
        return FromSpan(buffer);
    }


    public static void EncodePacket(ref PacketWriter w, ref T value)
    {
        ref var first = ref Unsafe.As<T, byte>(ref value);
        ReadOnlySpan<byte> span = MemoryMarshal.CreateReadOnlySpan(ref first, Size);
        w.WriteBytes(span);
    }

    public static T DecodePacket(ref PacketReader r)
    {
        return FromSequence(r.ReadBytes(Size));
    }
}

[InlineArray(16)]
public struct MachineId : IEncodePacket, IDecodePacket<MachineId>
{
    private byte _element0;

    public void EncodePacket(ref PacketWriter w) => UnsafePacketPod<MachineId>.EncodePacket(ref w, ref this);

    public static MachineId DecodePacket(ref PacketReader reader) =>
        UnsafePacketPod<MachineId>.DecodePacket(ref reader);
}

[InlineArray(8)]
public struct ClientKey : IEncodePacket, IDecodePacket<ClientKey>
{
    private byte _element0;

    public ClientKey(byte[] key)
    {
        if (key.Length != 8)
            throw new ArgumentException("Key must be exactly 8 bytes long.", nameof(key));

        var span = MemoryMarshal.CreateSpan(ref _element0, 8);
        key.CopyTo(span);
    }

    public static ClientKey GenerateRandom()
    {
        var key = new byte[8];
        Random.Shared.NextBytes(key);
        return new ClientKey(key);
    }

    public void EncodePacket(ref PacketWriter w) => UnsafePacketPod<ClientKey>.EncodePacket(ref w, ref this);

    public static ClientKey DecodePacket(ref PacketReader reader) =>
        UnsafePacketPod<ClientKey>.DecodePacket(ref reader);
}

[InlineArray(13)]
public struct NameString : IEncodePacket, IDecodePacket<NameString>
{
    public static NameString Empty => new([]);
    private byte _element0;

    public NameString(byte[] name)
    {
        if (name.Length != 13)
            throw new ArgumentException("Name string must be exactly 8 bytes long.", nameof(name));

        var span = MemoryMarshal.CreateSpan(ref _element0, 13);
        name.CopyTo(span);
    }


    public NameString(string name)
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        var bytes = Encoding.ASCII.GetBytes(name);
        if (bytes.Length > 13)
            throw new ArgumentException("Name string must be at most 13 ASCII bytes.", nameof(name));

        var span = MemoryMarshal.CreateSpan(ref _element0, 13);
        span.Clear(); // pad with zeroes
        bytes.CopyTo(span);
    }

    public void EncodePacket(ref PacketWriter w) => UnsafePacketPod<NameString>.EncodePacket(ref w, ref this);

    public static NameString DecodePacket(ref PacketReader reader) =>
        UnsafePacketPod<NameString>.DecodePacket(ref reader);
}

public record struct CheckPasswordReq : IDecodePacket<CheckPasswordReq>, IEncodePacket
{
    public string Id { get; set; }
    public string Password { get; set; }
    public MachineId MachineId { get; set; }
    public uint GameRoomClient { get; set; }
    public byte StartMode { get; set; }
    public byte U1 { get; set; }
    public byte U2 { get; set; }
    public uint PartnerCode { get; set; }

    public static CheckPasswordReq DecodePacket(ref PacketReader reader)
    {
        return new CheckPasswordReq
        {
            Id = reader.ReadString(),
            Password = reader.ReadString(),
            MachineId = reader.Read<MachineId>(),
            GameRoomClient = reader.ReadUInt(),
            StartMode = reader.ReadByte(),
            U1 = reader.ReadByte(),
            U2 = reader.ReadByte(),
            PartnerCode = reader.ReadUInt()
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteString(Id);
        w.WriteString(Password);
        w.Write(MachineId);
        w.WriteUInt(GameRoomClient);
        w.WriteByte(StartMode);
        w.WriteByte(U1);
        w.WriteByte(U2);
        w.WriteUInt(PartnerCode);
    }
}

[Flags]
public enum GradeCode : byte
{
    Normal = 0,
    Admin = 1 << 0,
    FakeAdmin = 1 << 5
}

[Flags]
public enum SubGradeCode : ushort
{
    Normal = 0,
    PrimaryTrace = 1 << 0,
    SecondaryTrace = 1 << 1,
    Admin = 1 << 2,
    MobMovementObserver = 1 << 3,
    Manager = 1 << 4,
    SuperGm = 1 << 5,
    Gm = 1 << 6,
    UserGm = 1 << 7,
    Tester = 1 << 8
}

public enum RegStateId : byte
{
    Registered0 = 0,
    Registered1 = 1,
    Verify2 = 2,
    Verify3 = 3
}

public enum LoginOption : byte
{
    EnableSecondaryPassword,
    CheckSecondaryPassword,
    NoSecondaryPassword1,
    NoSecondaryPassword2,
}

public enum OptionGender : byte
{
    Male = 0,
    Female = 1,
    NotSet = 10
}

public enum Gender : byte
{
    Male = 0,
    Female = 1,
    NotSet = 10
}

public abstract class LoginEnumConverter : IEnumConverter<LoginOption>, IEnumConverter<RegStateId>,
    IEnumConverter<OptionGender>, IEnumConverter<Gender>, IEnumConverter<WorldState>, IEnumConverter<StartMode>
{
    public static bool TryFromByte(byte value, out LoginOption result)
    {
        result = value switch
        {
            0 => LoginOption.EnableSecondaryPassword,
            1 => LoginOption.CheckSecondaryPassword,
            2 => LoginOption.NoSecondaryPassword1,
            3 => LoginOption.NoSecondaryPassword2,
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value: {value}")
        };
        return true;
    }

    public static bool TryFromByte(byte value, out RegStateId result)
    {
        result = value switch
        {
            0 => RegStateId.Registered0,
            1 => RegStateId.Registered1,
            2 => RegStateId.Verify2,
            3 => RegStateId.Verify3,
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value: {value}")
        };
        return true;
    }

    public static bool TryFromByte(byte value, out OptionGender result)
    {
        result = value switch
        {
            0 => OptionGender.Male,
            1 => OptionGender.Female,
            10 => OptionGender.NotSet,
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value: {value}")
        };
        return true;
    }

    public static bool TryFromByte(byte value, out Gender result)
    {
        result = value switch
        {
            0 => Gender.Male,
            1 => Gender.Female,
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value: {value}")
        };
        return true;
    }

    public static bool TryFromByte(byte value, out WorldState result)
    {
        result = value switch
        {
            0 => WorldState.Normale,
            1 => WorldState.Hot,
            2 => WorldState.New,
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value: {value}")
        };
        return true;
            
    }

    public static bool TryFromByte(byte value, out StartMode result)
    {
        result = value switch
        {
            0 => StartMode.Web,
            1 => StartMode.Unknown,
            2 => StartMode.GameLaunch,
            _ => throw new ArgumentOutOfRangeException(nameof(value), $"Invalid value: {value}")
        };
        return true;
    }
}

public struct AccountGrade(GradeCode code = GradeCode.Normal, SubGradeCode subCode = SubGradeCode.Normal)
    : IEncodePacket, IDecodePacket<AccountGrade>
{
    public GradeCode Code { get; set; } = code;
    public SubGradeCode SubCode { get; set; } = subCode;

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte((byte)Code);
        w.WriteUShort((ushort)SubCode);
    }

    public static AccountGrade DecodePacket(ref PacketReader r)
    {
        return new AccountGrade
        {
            Code = (GradeCode)r.ReadByte(),
            SubCode = (SubGradeCode)r.ReadUShort()
        };
    }
}

public struct AccountInfo : IEncodePacket, IDecodePacket<AccountInfo>
{
    public uint Id { get; set; }
    public Gender Gender { get; set; }
    public AccountGrade Grade { get; set; }
    public byte CountryId { get; set; }
    public string Name { get; set; }
    public byte PurchaseExp { get; set; }
    public byte ChatBlockReason { get; set; }
    public FileTime ChatBlockDate { get; set; }
    public FileTime RegistrationDate { get; set; }
    public uint NumChars { get; set; }


    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(Id);
        w.WriteByte((byte)Gender);
        w.Write(Grade);
        w.WriteByte(CountryId);
        w.WriteString(Name);
        w.WriteByte(PurchaseExp);
        w.WriteByte(ChatBlockReason);
        w.Write(ChatBlockDate);
        w.Write(RegistrationDate);
        w.WriteUInt(NumChars);
    }

    public static AccountInfo DecodePacket(ref PacketReader r)
    {
        return new AccountInfo
        {
            Id = r.ReadUInt(),
            Gender = r.ReadEnum<Gender, LoginEnumConverter>(),
            Grade = r.Read<AccountGrade>(),
            CountryId = r.ReadByte(),
            Name = r.ReadString(),
            PurchaseExp = r.ReadByte(),
            ChatBlockReason = r.ReadByte(),
            ChatBlockDate = r.ReadTime(),
            RegistrationDate = r.ReadTime(),
            NumChars = r.ReadUInt()
        };
    }
}

public struct GuestAccountInfo : IEncodePacket, IDecodePacket<GuestAccountInfo>
{
    public uint AccountId { get; set; }
    public OptionGender Gender { get; set; }
    public byte GradeCode { get; set; }
    public byte SubGradeCode { get; set; }
    public bool IsTestAcc { get; set; }
    public byte CountryId { get; set; }
    public string Name { get; set; }
    public byte PurchaseExp { get; set; }
    public byte ChatBlockReason { get; set; }
    public FileTime ChatBlockDate { get; set; }
    public FileTime RegistrationDate { get; set; }
    public uint NumChars { get; set; }
    public string GuestIdUrl { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(AccountId);
        w.WriteByte((byte)Gender);
        w.WriteByte(GradeCode);
        w.WriteByte(SubGradeCode);
        w.WriteBool(IsTestAcc);
        w.WriteByte(CountryId);
        w.WriteString(Name);
        w.WriteByte(PurchaseExp);
        w.WriteByte(ChatBlockReason);
        ChatBlockDate.EncodePacket(ref w);
        RegistrationDate.EncodePacket(ref w);
        w.WriteUInt(NumChars);
        w.WriteString(GuestIdUrl);
    }

    public static GuestAccountInfo DecodePacket(ref PacketReader r)
    {
        return new GuestAccountInfo
        {
            AccountId = r.ReadUInt(),
            Gender = r.ReadEnum<OptionGender, LoginEnumConverter>(),
            GradeCode = r.ReadByte(),
            SubGradeCode = r.ReadByte(),
            IsTestAcc = r.ReadBool(),
            CountryId = r.ReadByte(),
            Name = r.ReadString(),
            PurchaseExp = r.ReadByte(),
            ChatBlockReason = r.ReadByte(),
            ChatBlockDate = FileTime.DecodePacket(ref r),
            RegistrationDate = FileTime.DecodePacket(ref r),
            NumChars = r.ReadUInt(),
            GuestIdUrl = r.ReadString()
        };
    }
}

public struct LoginResultHeader : IEncodePacket, IDecodePacket<LoginResultHeader>
{
    public LoginResultHeader()
    {
    }

    public RegStateId Reg { get; set; } = RegStateId.Registered0;
    public uint Unknown { get; set; } = 0;

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte((byte)Reg);
        w.WriteUInt(Unknown);
    }

    public static LoginResultHeader DecodePacket(ref PacketReader r)
    {
        return new LoginResultHeader
        {
            Reg = r.ReadEnum<RegStateId, LoginEnumConverter>(),
            Unknown = r.ReadUInt()
        };
    }
}

public struct LoginInfo : IEncodePacket, IDecodePacket<LoginInfo>
{
    public bool SkipPin { get; set; }
    public LoginOption LoginOpt { get; set; }
    public ClientKey ClientKey { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteBool(SkipPin);
        w.WriteByte((byte)LoginOpt);
        ClientKey.EncodePacket(ref w);
    }

    public static LoginInfo DecodePacket(ref PacketReader r)
    {
        return new LoginInfo
        {
            SkipPin = r.ReadBool(),
            LoginOpt = r.ReadEnum<LoginOption, LoginEnumConverter>(),
            ClientKey = ClientKey.DecodePacket(ref r)
        };
    }
}

public abstract class CheckPasswordResp : IDecodePacket<CheckPasswordResp>, IPacketMessage
{
    public abstract byte Type { get; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(Type);
        EncodeInner(ref w);
    }

    protected abstract void EncodeInner(ref PacketWriter packetWriter);

    public static CheckPasswordResp DecodePacket(ref PacketReader r)
    {
        var type = r.ReadByte();
        return type switch
        {
            0 => CheckPasswordSuccess.Decode(ref r),
            2 => CheckPasswordBlockedIp.Decode(ref r),
            3 or 4 or 5 or 6 or 7 or 13 or 23 or 255 => CheckPasswordHeaderOnly.Decode(type, ref r),
            _ => throw new NotSupportedException($"Unknown CheckPasswordResp type: {type}")
        };
    }

    public static SendOpcodes Opcode => SendOpcodes.CheckPasswordResult;
}

public sealed class CheckPasswordSuccess : CheckPasswordResp
{
    public override byte Type => 0;

    public LoginResultHeader Hdr { get; set; }
    public AccountInfo AccountInfo { get; set; }
    public LoginInfo Info { get; set; }

    protected override void EncodeInner(ref PacketWriter w)
    {
        Hdr.EncodePacket(ref w);
        AccountInfo.EncodePacket(ref w);
        Info.EncodePacket(ref w);
    }

    public static CheckPasswordSuccess Decode(ref PacketReader r)
    {
        return new CheckPasswordSuccess
        {
            Hdr = LoginResultHeader.DecodePacket(ref r),
            AccountInfo = r.Read<AccountInfo>(),
            Info = r.Read<LoginInfo>()
        };
    }
}

public struct BlockedIp : IEncodePacket, IDecodePacket<BlockedIp>
{
    public LoginResultHeader Hdr { get; set; }
    public byte Reason { get; set; }
    public FileTime BanTime { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        w.Write(Hdr);
        w.WriteByte(Reason);
        w.Write(BanTime);
    }

    public static BlockedIp DecodePacket(ref PacketReader r)
    {
        return new BlockedIp
        {
            Hdr = LoginResultHeader.DecodePacket(ref r),
            Reason = r.ReadByte(),
            BanTime = r.ReadTime()
        };
    }
}

public sealed class CheckPasswordBlockedIp : CheckPasswordResp
{
    public override byte Type => 2;
    public BlockedIp BlockedIp { get; set; }

    protected override void EncodeInner(ref PacketWriter w)
    {
        BlockedIp.EncodePacket(ref w);
    }

    public static CheckPasswordBlockedIp Decode(ref PacketReader r)
    {
        return new CheckPasswordBlockedIp
        {
            BlockedIp = BlockedIp.DecodePacket(ref r)
        };
    }
}

public sealed class CheckPasswordHeaderOnly(byte type) : CheckPasswordResp
{
    public override byte Type { get; } = type;
    public LoginResultHeader Header { get; set; }

    protected override void EncodeInner(ref PacketWriter w)
    {
        Header.EncodePacket(ref w);
    }

    public static CheckPasswordHeaderOnly Decode(byte type, ref PacketReader r)
    {
        return new CheckPasswordHeaderOnly(type)
        {
            Header = LoginResultHeader.DecodePacket(ref r)
        };
    }
}

public struct Vec2(short x, short y) : IEncodePacket, IDecodePacket<Vec2>
{
    public short X { get; set; } = x;
    public short Y { get; set; } = y;

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteShort(X);
        w.WriteShort(Y);
    }

    public static Vec2 DecodePacket(ref PacketReader reader)
    {
        return new Vec2
        {
            X = reader.ReadShort(),
            Y = reader.ReadShort()
        };
    }
}

public struct ChannelItem : IEncodePacket, IDecodePacket<ChannelItem>
{
    public ChannelItem(string name, byte worldId, byte id)
    {
        Name = name;
        worldId = worldId;
        Id = id;
    }
    
    
    public string Name { get; set; }
    public uint UserNumber { get; set; } = 0;
    public byte WorldId { get; set; }
    public byte Id { get; set; }
    public bool IsAdultChannel { get; set; } = false;


    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteString(Name);
        w.WriteUInt(UserNumber);
        w.WriteByte(WorldId);
        w.WriteByte(Id);
        w.WriteBool(IsAdultChannel);
    }

    public static ChannelItem DecodePacket(ref PacketReader reader)
    {
        return new ChannelItem
        {
            Name = reader.ReadString(),
            UserNumber = reader.ReadUInt(),
            WorldId = reader.ReadByte(),
            Id = reader.ReadByte(),
            IsAdultChannel = reader.ReadBool()
        };
    }
}

public struct WorldBalloon : IEncodePacket, IDecodePacket<WorldBalloon>
{
    public Vec2 Position { get; set; }
    public string Message { get; set; }

    public void EncodePacket(ref PacketWriter w)
    {
        Position.EncodePacket(ref w);
        w.WriteString(Message);
    }

    public static WorldBalloon DecodePacket(ref PacketReader reader)
    {
        return new WorldBalloon
        {
            Position = Vec2.DecodePacket(ref reader),
            Message = reader.ReadString()
        };
    }
}

public enum WorldState : byte
{
    Normale = 0,
    Hot = 1,
    New = 2,
}

public struct WorldItem : IEncodePacket, IDecodePacket<WorldItem>
{
    public WorldItem(string name)
    {
        Name = name;
    }

    public string Name { get; set; }
    public WorldState State { get; set; } = WorldState.Normale;
    public string EventDesc { get; set; } = string.Empty;
    public ushort EventExp { get; set; } = 0;
    public ushort EventDropRate { get; set; } = 0;
    public bool BlockCharCreation { get; set; }
    public ShroomList<I8, ChannelItem> Channels { get; set; } = new(new List<ChannelItem>());
    public ShroomList<I16, WorldBalloon> Balloons { get; set; } = new(new List<WorldBalloon>());
    
    
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteString(Name);
        w.WriteByte((byte)State);
        w.WriteString(EventDesc);
        w.WriteUShort(EventExp);
        w.WriteUShort(EventDropRate);
        w.WriteBool(BlockCharCreation);
        Channels.EncodePacket(ref w);
        Balloons.EncodePacket(ref w);
    }

    public static WorldItem DecodePacket(ref PacketReader reader)
    {
        return new WorldItem
        {
            Name = reader.ReadString(),
            State = (WorldState)reader.ReadByte(),
            EventDesc = reader.ReadString(),
            EventExp = reader.ReadUShort(),
            EventDropRate = reader.ReadUShort(),
            BlockCharCreation = reader.ReadBool(),
            Channels = ShroomList<I8, ChannelItem>.DecodePacket(ref reader),
            Balloons = ShroomList<I16, WorldBalloon>.DecodePacket(ref reader)
        };
    }
}


public struct WorldInformationResponse : IDecodePacket<WorldInformationResponse>, IPacketMessage
{

    public static readonly WorldInformationResponse End = new()
    {
        WorldId = 255,
        World = null
    };
    
    public byte WorldId { get; set; }
    public WorldItem? World { get; set; }
    public static WorldInformationResponse DecodePacket(ref PacketReader reader)
    {
        var worldId = reader.ReadByte();
        return new WorldInformationResponse
        {
            WorldId = worldId,
            World = worldId != 255 ? reader.Read<WorldItem>() : null,
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(WorldId);
        World?.EncodePacket(ref w);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.WorldInformation;
}


public struct CheckUserLimitReq : IDecodePacket<CheckUserLimitReq>, IEncodePacket
{
    public byte WorldId { get; set; }
    public static CheckUserLimitReq DecodePacket(ref PacketReader reader)
    {
        return new CheckUserLimitReq
        {
            WorldId = reader.ReadByte()
        };
        
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(WorldId);
    }
}

public struct CheckUserLimitResponse : IDecodePacket<CheckUserLimitResponse>, IPacketMessage
{
    public CheckUserLimitResponse()
    {
    }

    public bool OverLimit { get; set; } = false;
    public byte PopulateLevel { get; set; } = 0;
    
    public static CheckUserLimitResponse DecodePacket(ref PacketReader reader)
    {
        return new CheckUserLimitResponse
        {
            OverLimit = reader.ReadBool(),
            PopulateLevel = reader.ReadByte()
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteBool(OverLimit);
        w.WriteByte(PopulateLevel);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.CheckUserLimitResult;
}

public enum StartMode: byte
{
    Web = 0,
    Unknown = 1,
    GameLaunch = 2,
}


public struct SystemInfo : IDecodePacket<SystemInfo>, IEncodePacket
{
    public SystemInfo()
    {
        MachineId = default;
    }

    public string Unknown { get; set; } = string.Empty;
    public MachineId MachineId { get; set; }
    public uint GameRoomClient { get; set; } = 0;
    public byte StartMode { get; set; } = 0;
    
    
    public static SystemInfo DecodePacket(ref PacketReader reader)
    {
        return new SystemInfo
        {
            Unknown = reader.ReadString(),
            MachineId = reader.Read<MachineId>(),
            GameRoomClient = reader.ReadUInt(),
            StartMode = reader.ReadByte()
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteString(Unknown);
        w.Write(MachineId);
        w.WriteUInt(GameRoomClient);
        w.WriteByte(StartMode);
    }
}

public record struct StartModeInfo : IDecodePacket<StartModeInfo>, IEncodePacket
{
    public StartModeInfo()
    {
    }

    public StartMode StartMode { get; set; } = StartMode.GameLaunch;
    public SystemInfo? SystemInfo { get; set; } = null;
    public static StartModeInfo DecodePacket(ref PacketReader reader)
    {
        var startMode = reader.ReadEnum<StartMode, LoginEnumConverter>();
        SystemInfo? systemInfo = startMode == StartMode.Unknown ? reader.Read<SystemInfo>() : null;
        return new StartModeInfo
        {
            StartMode = startMode,
            SystemInfo = systemInfo
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte((byte)StartMode);
        SystemInfo?.EncodePacket(ref w);
    }
}


public record struct SelectWorldReq : IDecodePacket<SelectWorldReq>, IEncodePacket
{
    public StartModeInfo StartModeInfo { get; set; }
    public byte WorldId { get; set; }
    public byte ChannelId { get; set; }
    public uint SaData { get; set; }
    
    public static SelectWorldReq DecodePacket(ref PacketReader reader)
    {
        return new SelectWorldReq
        {
            StartModeInfo = reader.Read<StartModeInfo>(),
            WorldId = reader.ReadByte(),
            ChannelId = reader.ReadByte(),
            SaData = reader.ReadUInt()
        };
    }

    public void EncodePacket(ref PacketWriter w)
    {
        StartModeInfo.EncodePacket(ref w);
        w.WriteByte(WorldId);
        w.WriteByte(ChannelId);
        w.WriteUInt(SaData);
    }
}

public record struct ItemId(int Id) : IEncodePacket, IDecodePacket<ItemId>
{
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteInt(Id);
    }
    
    public static ItemId DecodePacket(ref PacketReader reader)
    {
        return new ItemId
        {
            Id = reader.ReadInt()
        };
    }
}

public record struct AvatarEquips : IEncodePacket
{
    public AvatarEquips()
    {
        WeaponStickerId = new ItemId(0);
    }

    public ShroomIndexList<I8, ItemId> EquipIds { get; set; } = new(new List<(I8, ItemId)>());
    public ShroomIndexList<I8, ItemId> MaskedEquipIds { get; set; }= new(new List<(I8, ItemId)>());
    public ItemId WeaponStickerId { get; set; }


    public void EncodePacket(ref PacketWriter w)
    {
        EquipIds.EncodePacket(ref w);
        MaskedEquipIds.EncodePacket(ref w);
        WeaponStickerId.EncodePacket(ref w);
    }
}

public record struct AvatarData : IEncodePacket
{
    public Gender Gender { get; set; }
    public byte Skin { get; set; }
    public uint Face { get; set; }
    public bool Mega { get; set; }
    public uint Hair { get; set; }
    public AvatarEquips Equips { get; set; }
    public uint PetItem1 { get; set; }
    public uint PetItem2 { get; set; }
    public uint PetItem3 { get; set; }
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte((byte)Gender);
        w.WriteByte(Skin);
        w.WriteUInt(Face);
        w.WriteBool(Mega);
        w.WriteUInt(Hair);
        Equips.EncodePacket(ref w);
        w.WriteUInt(PetItem1);
        w.WriteUInt(PetItem2);
        w.WriteUInt(PetItem3);
    }
}





public record struct CharStat: IEncodePacket
{
    public CharStat(uint id)
    {
        Id = id;
    }

    public uint Id { get; set; }
    public NameString Name { get; set; } = NameString.Empty;
    public Gender Gender { get; set; } = Gender.Male;
    public byte Skin { get; set; } = 0;
    public uint Face { get; set; } = 0;
    public uint Hair { get; set; } = 0;
    public ulong Pet1 { get; set; } = 0;
    public ulong Pet2 { get; set; } = 0;
    public ulong Pet3 { get; set; } = 0;
    public byte Level { get; set; } = 0;
    public ushort Job { get; set; } = 0;
    public ushort Str { get; set; } = 0;
    public ushort Dex { get; set; } = 0;
    public ushort Int { get; set; } = 0;
    public ushort Luk { get; set; } = 0;
    public uint Hp { get; set; } = 0;
    public uint MaxHp { get; set; } = 0;
    public uint Mp { get; set; } = 0;
    public uint MaxMp { get; set; } = 0;
    public ushort Ap { get; set; } = 0;
    public ushort Sp { get; set; } = 0;
    public int Exp { get; set; } = 0;
    public ushort Fame { get; set; } = 0;
    public uint TempExp { get; set; } = 0;
    public uint FieldId { get; set; } = 0;
    public byte Portal { get; set; } = 0;
    public uint PlayTime { get; set; } = 0;
    public ushort SubJob { get; set; } = 0;
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(Id);
        w.Write(Name);
        w.WriteByte((byte)Gender);
        w.WriteByte(Skin);
        w.WriteUInt(Face);
        w.WriteUInt(Hair);
        w.WriteULong(Pet1);
        w.WriteULong(Pet2);
        w.WriteULong(Pet3);
        w.WriteByte(Level);
        w.WriteUShort(Job);
        w.WriteUShort(Str);
        w.WriteUShort(Dex);
        w.WriteUShort(Int);
        w.WriteUShort(Luk);
        w.WriteUInt(Hp);
        w.WriteUInt(MaxHp);
        w.WriteUInt(Mp);
        w.WriteUInt(MaxMp);
        w.WriteUShort(Ap);
        w.WriteUShort(Sp);
        w.WriteInt(Exp);
        w.WriteUShort(Fame);
        w.WriteUInt(TempExp);
        w.WriteUInt(FieldId);
        w.WriteByte(Portal);
        w.WriteUInt(PlayTime);
        w.WriteUShort(SubJob);
    }
}


public record struct RankInfo() : IEncodePacket
{
    public uint WorldRank { get; set; } = 0;
    public uint RankMove { get; set; } = 0;
    public uint JobRank { get; set; } = 0;
    public uint JobRankMove { get; set; } = 0;
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(WorldRank);
        w.WriteUInt(RankMove);
        w.WriteUInt(JobRank);
        w.WriteUInt(JobRankMove);
    }
}


public record struct CharView : IEncodePacket
{
    public CharStat Stats { get; set; }
    public AvatarData Avatar { get; set; }
    public void EncodePacket(ref PacketWriter w)
    {
        Stats.EncodePacket(ref w);
        Avatar.EncodePacket(ref w);
    }
}

public record CharRankView : IEncodePacket, IDecodePacket<CharRankView>
{
    public CharView View { get; set; }
    public RankInfo? RankInfo { get; set; }
    public void EncodePacket(ref PacketWriter w)
    {
        View.EncodePacket(ref w);
        w.WriteByte(0); //TODO vac byte?

        if (RankInfo != null)
        {
            //TODO optional
            w.WriteByte(1);
            RankInfo?.EncodePacket(ref w);
        }
        else
        {
            w.WriteByte(0);
        }
    }

    public static CharRankView DecodePacket(ref PacketReader reader)
    {
        throw new NotImplementedException();
    }
}


public record struct CharViewListResp(): IPacketMessage
{
    public ShroomList<I8, CharRankView> Characters { get; set; } = new(new List<CharRankView>());
    public LoginOption LoginOption { get; set; } = LoginOption.NoSecondaryPassword1;
    public uint Slots { get; set; } = 0;
    public uint BuySlots { get; set; } = 0;
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0); // Success
        Characters.EncodePacket(ref w);
        w.WriteByte((byte)LoginOption);
        w.WriteUInt(Slots);
        w.WriteUInt(BuySlots);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.SelectWorldResult; //TODO should be it's own struct
}


public record struct HardwareInfo() : IDecodePacket<HardwareInfo>
{
    public string MacAddress { get; set; } = string.Empty;
    public string HddSerialNumber { get; set; } = string.Empty;
    public static HardwareInfo DecodePacket(ref PacketReader reader)
    {
        return new HardwareInfo
        {
            MacAddress = reader.ReadString(),
            HddSerialNumber = reader.ReadString()
        };
    }
}

public record struct SelectCharRequest : IDecodePacket<SelectCharRequest>
{
    public SelectCharRequest()
    {
        HardwareInfo = default;
    }

    public uint CharId { get; set; } = 0;
    public HardwareInfo HardwareInfo { get; set; }
    
    public static SelectCharRequest DecodePacket(ref PacketReader reader)
    {
        return new SelectCharRequest
        {
            CharId = reader.ReadUInt(),
            HardwareInfo = reader.Read<HardwareInfo>()
        };
    }
}


public record struct MigrateInfo : IEncodePacket
{
    public uint Address4 { get; set; }
    public ushort Port { get; set; }
    public uint CharId { get; set; }
    public bool Premium { get; set; } = false;
    public uint PremiumArgument { get; set; } = 0;

    public MigrateInfo(uint addr, ushort port, uint charId)
    {
        Address4 = addr;
        Port = port;
        CharId = charId;
    }

    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteUInt(Address4);
        w.WriteUShort(Port);
        w.WriteUInt(CharId);
        w.WriteBool(Premium);
        w.WriteUInt(PremiumArgument);
    }
}

public record struct SelectCharResponse : IPacketMessage
{
    public MigrateInfo MigrateInfo { get; set; }
    public void EncodePacket(ref PacketWriter w)
    {
        w.WriteByte(0);//No error
        w.WriteByte(0); // Success
        MigrateInfo.EncodePacket(ref w);
    }

    public static SendOpcodes Opcode { get; } = SendOpcodes.SelectCharacterResult;
}