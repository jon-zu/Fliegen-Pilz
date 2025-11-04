using System.Net;
using FliegenPilz.Net;
using FliegenPilz.Proto;
using Microsoft.Extensions.Logging;

namespace FliegenPilz;

public class LoginHandler(ILogger<LoginHandler> logger) : IRpcHandler
{
    private static T DecodeMsg<T>(ref PacketReader reader)
        where T : IDecodePacket<T>
    {
        //TODO log errors during decoding...
        return T.DecodePacket(ref reader);
    }

    public Task HandlePacket(PacketReader reader, RpcContext ctx, CancellationToken ct)
    {
        logger.LogInformation("LoginPacket: {}", reader.ToString());

        var op = RecvOpcodesExt.GetRecvOpcode(reader.ReadShort());
        switch (op)
        {
            case RecvOpcodes.CreateSecurityHandle:
                // Unhandled
                return Task.CompletedTask;
            case RecvOpcodes.CheckPassword:
                return HandleCheckPassword(DecodeMsg<CheckPasswordReq>(ref reader), ctx, ct);
            case RecvOpcodes.WorldRequest:
                return HandleWorldRequest(ctx, ct);
            case RecvOpcodes.CheckUserLimit:
                return HandleCheckUserLimit(DecodeMsg<CheckUserLimitReq>(ref reader), ctx, ct);
            case RecvOpcodes.SelectWorld:
                return HandleSelectWorld(DecodeMsg<SelectWorldReq>(ref reader), ctx, ct);
            case RecvOpcodes.SelectCharacter:
                return HandleSelectChar(DecodeMsg<SelectCharRequest>(ref reader), ctx, ct);

            default:
                logger.LogInformation("Unhandled opcode: {opcode}", op);
                return Task.CompletedTask;
        }
    }

    public void HandleException(Exception e)
    {
        logger.LogError(e, "Exception in LoginHandler");
    }


    private async Task HandleSelectChar(SelectCharRequest req, RpcContext ctx, CancellationToken ct)
    {
        //TODO fix that up
        var addr = IPAddress.Loopback;
        var bytes = addr.GetAddressBytes();
        var addr4 = (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16) | (bytes[3] << 24));
        
        // Convert to uint
        ushort port = 8485;

        await ctx.ReplyAsync(new SelectCharResponse
        {
            MigrateInfo = new MigrateInfo
            {
                Address4 = addr4,
                Port = port,
                CharId = 1,
                Premium = false,
                PremiumArgument = 0
            }
        }, ct);

        logger.LogInformation("Selected char: {} - Sending Migration", req.CharId);
    }

    private async Task HandleSelectWorld(SelectWorldReq req, RpcContext ctx, CancellationToken ct)
    {
        var character = new CharView
        {
            Avatar = new AvatarData
            {
                Equips = new AvatarEquips(),
                Face = 20000,
                Gender = Gender.Male,
                Hair = 30000,
                Mega = false,
                PetItem1 = 0,
                PetItem2 = 0,
                PetItem3 = 0,
                Skin = 0,
            },
            Stats = new CharStat
            {
                Id = 1,
                Ap = 0,
                Sp = 0,
                Exp = 0,
                Fame = 0,
                TempExp = 0,
                FieldId = 100000,
                Portal = 0,
                PlayTime = 0,
                SubJob = 0,
                Name = new NameString("Test123"),
                Gender = Gender.Male,
                Skin = 0,
                Face = 20000,
                Hair = 30000,
                Pet1 = 0,
                Pet2 = 0,
                Pet3 = 0,
                Level = 10,
                Job = 100,
                Str = 10,
                Dex = 10,
                Luk = 10,
                Hp = 10,
                MaxHp = 100,
                Mp = 10,
                MaxMp = 100,
                Int = 10,
            }
        };

        var resp = new CharViewListResp
        {
            Characters = new ShroomList<I8, CharRankView>(new List<CharRankView>()),
            LoginOption = LoginOption.NoSecondaryPassword1,
            Slots = 0,
            BuySlots = 0
        };

        resp.Characters.Items.Add(new CharRankView()
        {
            View = character,
            RankInfo = null
        });


        await ctx.ReplyAsync(resp, ct);


        logger.LogInformation("SelectWorldReq: {}", req.ToString());
        return;
    }

    private async Task HandleWorldRequest(RpcContext ctx, CancellationToken ct)
    {
        logger.LogInformation("WorldRequest");
        var world = new WorldItem("World1");
        world.Channels.Items.Add(new ChannelItem("Ch1", 1, 1));
        await ctx.ReplyAsync(new WorldInformationResponse
        {
            WorldId = 1,
            World = world
        }, ct);
        await ctx.ReplyAsync(WorldInformationResponse.End, ct);
    }

    private async Task HandleCheckUserLimit(CheckUserLimitReq req, RpcContext ctx, CancellationToken ct)
    {
        logger.LogInformation("CheckUserLimit");

        await ctx.ReplyAsync(new CheckUserLimitResponse(), ct);
    }

    private async Task HandleCheckPassword(CheckPasswordReq req, RpcContext ctx, CancellationToken ct)
    {
        // Censor Id + Password for now
        //req.Id = "CensoredId";
        req.Password = "CensoredPw";
        logger.LogInformation("CheckPasswordReq: {req}", req.ToString());

        await ctx.ReplyAsync(new CheckPasswordSuccess
        {
            Hdr = new LoginResultHeader(),
            AccountInfo = new AccountInfo()
            {
                Id = 1,
                Gender = Gender.Male,
                ChatBlockDate = FileTime.Zero,
                ChatBlockReason = 0,
                CountryId = 1,
                Grade = new AccountGrade(),
                Name = req.Id,
                NumChars = 3,
                PurchaseExp = 0,
                RegistrationDate = FileTime.Now(),
            },
            Info = new LoginInfo()
            {
                ClientKey = ClientKey.GenerateRandom(),
                LoginOpt = LoginOption.NoSecondaryPassword1,
                SkipPin = true
            }
        }, ct);
    }
}

public class LoginServer(ILoggerFactory loggerFactory) : IRpcServerHandler<LoginHandler>
{
    public Task<RpcClient<LoginHandler>> AcceptClientAsync(NetClient client, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger<LoginHandler>();
        return Task.FromResult(new RpcClient<LoginHandler>(client, new LoginHandler(logger)));
    }
}