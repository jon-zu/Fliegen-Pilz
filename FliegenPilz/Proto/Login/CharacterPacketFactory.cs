using System;
using System.Collections.Generic;
using FliegenPilz.Data;

namespace FliegenPilz.Proto.Login;

public static class CharacterPacketFactory
{
    public static CharStat ToCharStat(this CharacterEntity entity)
    {
        return new CharStat
        {
            Id = (uint)entity.Id.Value,
            Name = new NameString(entity.Name ?? string.Empty),
            Level = (byte)Math.Clamp(entity.Level, 1, byte.MaxValue),
            Job = 100,
            Str = 4,
            Dex = 4,
            Int = 4,
            Luk = 4,
            Hp = 50,
            MaxHp = 50,
            Mp = 50,
            MaxMp = 50,
            FieldId = (uint)entity.MapId,
            Portal = 0,
            PlayTime = 0
        };
    }

    public static AvatarData ToDefaultAvatar(this CharacterEntity entity)
    {
        return new AvatarData
        {
            Gender = Gender.Male,
            Skin = 0,
            Face = 20000,
            Hair = 30000,
            Mega = false,
            Equips = new AvatarEquips(),
            PetItem1 = 0,
            PetItem2 = 0,
            PetItem3 = 0
        };
    }

    public static CharRankView ToRankView(this CharacterEntity entity)
    {
        return new CharRankView
        {
            View = new CharView
            {
                Stats = entity.ToCharStat(),
                Avatar = entity.ToDefaultAvatar()
            },
            RankInfo = null
        };
    }

    public static CharViewListResp ToCharacterListResponse(this IEnumerable<CharacterEntity> characters, LoginOption option = LoginOption.NoSecondaryPassword1)
    {
        var list = characters is ICollection<CharacterEntity> collection
            ? new List<CharacterEntity>(collection)
            : new List<CharacterEntity>(characters);

        var response = new CharViewListResp()
        {
            LoginOption = option,
            Slots = (uint)Math.Max(list.Count, 3),
            BuySlots = 0
        };

        foreach (var character in list)
        {
            response.Characters.Items.Add(character.ToRankView());
        }

        return response;
    }
}
