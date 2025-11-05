using System;
using System.Net;
using FliegenPilz.Data;
using FliegenPilz.Net;
using FliegenPilz.Proto;
using FliegenPilz.Proto.Login;
using FliegenPilz.Util;
using FliegenPilz.World;
using Microsoft.Extensions.Logging;

namespace FliegenPilz;

public class LoginHandler : IRpcHandler
{
    private readonly ILogger<LoginHandler> _logger;
    private readonly SessionManager _sessionManager;
    private AccountId? _accountId;

    public LoginHandler(ILogger<LoginHandler> logger, SessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    private static T DecodeMsg<T>(ref PacketReader reader)
        where T : IDecodePacket<T>
    {
        //TODO log errors during decoding...
        return T.DecodePacket(ref reader);
    }

    public Task HandlePacket(PacketReader reader, RpcContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("LoginPacket: {}", reader.ToString());

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
                _logger.LogInformation("Unhandled opcode: {opcode}", op);
                return Task.CompletedTask;
        }
    }

    public void HandleException(Exception e)
    {
        _logger.LogError(e, "Exception in LoginHandler");
    }


    private async Task HandleSelectChar(SelectCharRequest req, RpcContext ctx, CancellationToken ct)
    {
        if (_accountId is null)
            throw new InvalidOperationException("Account not authenticated");

        var accountId = _accountId.Value;
        var characterId = new CharacterId((int)req.CharId);
        var endpoint = ctx.Client.RemoteEndPoint ?? new IPEndPoint(IPAddress.Loopback, 0);

        var ticket = _sessionManager.CreateMigrationTicket(accountId, characterId, endpoint);

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
                CharId = (uint)characterId.Value,
                Premium = false,
                PremiumArgument = 0,
                ClientSessionId = ticket.ClientSessionId
            }
        }, ct);

        _logger.LogInformation("Selected char: {} - Sending Migration", req.CharId);
    }

    private async Task HandleSelectWorld(SelectWorldReq req, RpcContext ctx, CancellationToken ct)
    {
        if (_accountId is null)
            throw new InvalidOperationException("Account not authenticated");

        var accountId = _accountId.Value;
        var characters = await _sessionManager.GetCharactersAsync(accountId, ct);

        await ctx.ReplyAsync(characters.ToCharacterListResponse(), ct);


        _logger.LogInformation("SelectWorldReq: {}", req.ToString());
        return;
    }

    private async Task HandleWorldRequest(RpcContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("WorldRequest");
        var world = new WorldItem("World1");
        world.Channels.Add(new ChannelItem("Ch1", 1, 1));
        await ctx.ReplyAsync(new WorldInformationResponse
        {
            WorldId = 1,
            World = world
        }, ct);
        await ctx.ReplyAsync(WorldInformationResponse.End, ct);
    }

    private async Task HandleCheckUserLimit(CheckUserLimitReq req, RpcContext ctx, CancellationToken ct)
    {
        _logger.LogInformation("CheckUserLimit");

        await ctx.ReplyAsync(new CheckUserLimitResponse(), ct);
    }

    private async Task HandleCheckPassword(CheckPasswordReq req, RpcContext ctx, CancellationToken ct)
    {
        req.Password = "CensoredPw";
        _logger.LogInformation("CheckPasswordReq: {req}", req.ToString());

        var accountId = await _sessionManager.GetOrCreateAccountAsync(req.Id, ct);
        _accountId = accountId;
        await _sessionManager.EnsureDefaultCharacterAsync(accountId, ct);
        var characters = await _sessionManager.GetCharactersAsync(accountId, ct);

        await ctx.ReplyAsync(new CheckPasswordSuccess
        {
            Hdr = new LoginResultHeader(),
            AccountInfo = new AccountInfo()
            {
                Id = (uint)accountId.Value,
                Gender = Gender.Male,
                ChatBlockDate = FileTime.Zero,
                ChatBlockReason = 0,
                CountryId = 1,
                Grade = new AccountGrade(),
                Name = req.Id,
                NumChars = (uint)characters.Count,
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

public class LoginServer(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRpcServerHandler<LoginHandler>
{
    public Task<RpcClient<LoginHandler>> AcceptClientAsync(NetClient client, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger<LoginHandler>();
        return Task.FromResult(new RpcClient<LoginHandler>(client, new LoginHandler(logger, sessionManager)));
    }
}
