using System;
using FliegenPilz.Net;

namespace FliegenPilz.Proto.Login;

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

public enum WorldState : byte
{
    Normal = 0,
    HighlyPopulated = 1,
    Full = 2
}

public enum StartMode : byte
{
    Normal = 0,
    ResetPicture = 1
}

public abstract class LoginEnumConverter :
    IEnumConverter<LoginOption>,
    IEnumConverter<RegStateId>,
    IEnumConverter<OptionGender>,
    IEnumConverter<Gender>,
    IEnumConverter<WorldState>,
    IEnumConverter<StartMode>
{
    public static bool TryFromByte(byte value, out LoginOption result)
    {
        result = value switch
        {
            0 => LoginOption.EnableSecondaryPassword,
            1 => LoginOption.CheckSecondaryPassword,
            2 => LoginOption.NoSecondaryPassword1,
            3 => LoginOption.NoSecondaryPassword2,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
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
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
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
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        return true;
    }

    public static bool TryFromByte(byte value, out Gender result)
    {
        result = value switch
        {
            0 => Gender.Male,
            1 => Gender.Female,
            10 => Gender.NotSet,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        return true;
    }

    public static bool TryFromByte(byte value, out WorldState result)
    {
        result = value switch
        {
            0 => WorldState.Normal,
            1 => WorldState.HighlyPopulated,
            2 => WorldState.Full,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        return true;
    }

    public static bool TryFromByte(byte value, out StartMode result)
    {
        result = value switch
        {
            0 => StartMode.Normal,
            1 => StartMode.ResetPicture,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
        return true;
    }
}
