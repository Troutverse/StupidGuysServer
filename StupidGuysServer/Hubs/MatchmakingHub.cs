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
        Console.WriteLine($"Client connected: {connectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string connectionId = Context.ConnectionId;
        Console.WriteLine($"Client disconnected: {connectionId}");

        var lobby = _lobbiesManager.RemovePlayerFromAllLobbies(connectionId);

        if (lobby != null) await NotifyLobbyUpdated(lobby);

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<MatchmakingResult> FindOrCreateLobby(int maxPlayers)
    {
        string connectionId = Context.ConnectionId;

        var lobby = _lobbiesManager.FindLobby();

        if (lobby == null)
        {
            lobby = _lobbiesManager.CreateLobby(maxPlayers);
            Console.WriteLine($"Created new lobby {lobby.Id}");
            //lobby.GameServerIP = "92c613f02fc3.pr.edgegap.net";
            //lobby.GameServerPort = 31145;  

            lobby.GameServerIP = "127.0.0.1";
            lobby.GameServerPort = 7777;
            lobby.IsGameServerAllocated = true;
            Console.WriteLine($"Allocated game server: {lobby.GameServerIP}:{lobby.GameServerPort}");
        }

        if (lobby.AddMember(connectionId, out int remainMemberCount))
        {
            await Groups.AddToGroupAsync(connectionId, $"lobby_{lobby.Id}");

            Console.WriteLine($"{connectionId} joined lobby {lobby.Id} ({lobby.MemberCount}/{maxPlayers})");

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
            throw new HubException("Failed to join lobby");
        }
    }

    public async Task<bool> LeaveLobby(int lobbyId)
    {
        string connectionId = Context.ConnectionId;
        var lobby = _lobbiesManager.GetLobby(lobbyId);
        
        bool removed = lobby.RemoveMember(connectionId);

        if (removed)
        {
            await Groups.RemoveFromGroupAsync(connectionId, $"lobby_{lobby.Id}");

            Console.WriteLine($"[SignalR] {connectionId} left lobby {lobbyId}. Remaining: {lobby.MemberCount}/{lobby.MaxPlayers}");

            if (lobby.MemberCount == 0)
            {
                _lobbiesManager.RemoveLobby(lobbyId);
                Console.WriteLine($"[SignalR] Lobby {lobbyId} is empty and has been removed");
            }
            else
            {
                await NotifyLobbyUpdated(lobby);
            }

            return true;
        }
        else
        {
            Console.WriteLine($"[SignalR] Failed to remove {connectionId} from lobby {lobbyId}");
            return false;
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

        await Clients.Group($"lobby_{lobby.Id}")
        .SendAsync("LobbyUpdated", status);
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