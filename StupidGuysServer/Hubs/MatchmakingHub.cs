using Microsoft.AspNetCore.SignalR;
using StupidGuysServer.Models;
using StupidGuysServer.Services;
using System;
using System.Threading.Tasks;

public class MatchmakingHub : Hub
{
    private readonly LobbiesManager _lobbiesManager;

    public MatchmakingHub(LobbiesManager lobbiesManager)
    {
        _lobbiesManager = lobbiesManager;
    }

    public override async Task OnConnectedAsync()
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] Client connected: {connectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] Client disconnected: {connectionId}");

        var lobby = _lobbiesManager.RemovePlayerFromAllLobbies(connectionId);

        if (lobby != null)
        {
            Console.WriteLine($"[SignalR] Removed {connectionId} from lobby {lobby.Id}");

            await NotifyLobbyUpdated(lobby);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<MatchmakingResult> FindOrCreateLobby(int maxPlayers)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"[SignalR] {connectionId} requested FindOrCreateLobby (maxPlayers: {maxPlayers})");

        var lobby = _lobbiesManager.FindAvailableLobby();

        if (lobby == null)
        {
            lobby = _lobbiesManager.CreateLobby(maxPlayers);
            Console.WriteLine($"[SignalR] Created new lobby {lobby.Id}");
            //lobby.GameServerIP = "92c613f02fc3.pr.edgegap.net";
            //lobby.GameServerPort = 31145;  

            lobby.GameServerIP = "127.0.0.1";
            lobby.GameServerPort = 7777;
            lobby.IsGameServerAllocated = true;
            Console.WriteLine($"[SignalR] Allocated game server: {lobby.GameServerIP}:{lobby.GameServerPort}");
        }

        if (lobby.TryAddMember(connectionId, out int remainMemberCount))
        {
            await Groups.AddToGroupAsync(connectionId, GetLobbyGroupName(lobby.Id));

            Console.WriteLine($"[SignalR] {connectionId} joined lobby {lobby.Id} ({lobby.MemberCount}/{maxPlayers})");

            await NotifyLobbyUpdated(lobby);

            return new MatchmakingResult
            {
                LobbyId = lobby.Id,
                GameServerIP = lobby.GameServerIP,
                GameServerPort = lobby.GameServerPort,
                Success = true
            };
        }
        else
        {
            Console.WriteLine($"[SignalR] Failed to add {connectionId} to lobby {lobby.Id}");
            throw new HubException("Failed to join lobby");
        }
    }

    public LobbyStatus GetLobbyStatus(int lobbyId)
    {
        var lobby = _lobbiesManager.GetLobby(lobbyId);

        if (lobby == null)
        {
            throw new HubException($"Lobby {lobbyId} not found");
        }

        return new LobbyStatus
        {
            Id = lobby.Id,
            CurrentPlayers = lobby.MemberCount,
            MaxPlayers = lobby.MaxPlayers,
            IsFull = lobby.IsFull
        };
    }

    private async Task NotifyLobbyUpdated(Lobby lobby)
    {
        var status = new LobbyStatus
        {
            Id = lobby.Id,
            CurrentPlayers = lobby.MemberCount,
            MaxPlayers = lobby.MaxPlayers,
            IsFull = lobby.IsFull
        };

        await Clients.Group(GetLobbyGroupName(lobby.Id))
        .SendAsync("LobbyUpdated", status);

        Console.WriteLine($"[SignalR] Notified lobby {lobby.Id} update: {status.CurrentPlayers}/{status.MaxPlayers}");
    }

    private string GetLobbyGroupName(int lobbyId)
    {
        return $"lobby_{lobbyId}";
    }
}

public class LobbyStatus
{
    public int Id { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public bool IsFull { get; set; }
}

public class MatchmakingResult
{
    public int LobbyId { get; set; }
    public string? GameServerIP { get; set; }
    public int GameServerPort { get; set; }
    public bool Success { get; set; }
}