using Microsoft.AspNetCore.SignalR;
using StupidGuysServer.Models;
using StupidGuysServer.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

public class MatchmakingHub : Hub
{
    private readonly LobbiesManager _lobbiesManager;
    private readonly PlayFabAllocationService _playFabService;

    public MatchmakingHub(LobbiesManager lobbiesManager, PlayFabAllocationService playFabService)
    {
        _lobbiesManager = lobbiesManager;
        _playFabService = playFabService;
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

        if (lobby != null)
        {
            await NotifyLobbyUpdated(lobby);

            // PlayFab 서버 종료 (로비가 비었을 때)
            if (lobby.MemberCount == 0 && !string.IsNullOrEmpty(lobby.PlayFabSessionId))
            {
                try
                {
                    await _playFabService.ShutdownServer(lobby.PlayFabSessionId);
                    Console.WriteLine($"[PlayFab] Server shutdown requested: {lobby.PlayFabSessionId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PlayFab] Failed to shutdown server: {ex.Message}");
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task<MatchmakingResult> FindOrCreateLobby(int maxPlayers)
    {
        string connectionId = Context.ConnectionId;

        var lobby = _lobbiesManager.FindLobby();

        if (lobby == null)
        {
            lobby = _lobbiesManager.CreateLobby(maxPlayers);
            lobby.CreatedAt = DateTime.UtcNow;

            // 고유 SessionId 생성 (LobbyId + Timestamp)
            lobby.PlayFabSessionId = Guid.NewGuid().ToString();

            Console.WriteLine($"Created new lobby {lobby.Id}");

            // ✅ PlayFab 서버 할당
            try
            {
                Console.WriteLine($"[PlayFab] Requesting server allocation...");

                var allocation = await _playFabService.RequestServer(lobby.PlayFabSessionId);

                lobby.GameServerIP = allocation.IPV4Address;
                lobby.GameServerPort = allocation.Port;
                lobby.IsGameServerAllocated = true;

                Console.WriteLine($"[PlayFab] ✅ Server allocated successfully!");
                Console.WriteLine($"[PlayFab] SessionId: {lobby.PlayFabSessionId}");
                Console.WriteLine($"[PlayFab] IP: {lobby.GameServerIP}");
                Console.WriteLine($"[PlayFab] Port: {lobby.GameServerPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayFab] ❌ Failed to allocate server: {ex.Message}");
                Console.WriteLine($"[PlayFab] Falling back to Render.com server...");

                // Fallback: Render.com 서버 사용 (기존 배포된 서버)
                lobby.GameServerIP = "your-render-server.onrender.com"; // 실제 Render.com 주소로 변경
                lobby.GameServerPort = 7777;
                lobby.IsGameServerAllocated = true;
            }
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

        if (lobby == null)
        {
            Console.WriteLine($"[SignalR] Lobby {lobbyId} not found");
            return false;
        }

        bool removed = lobby.RemoveMember(connectionId);

        if (removed)
        {
            await Groups.RemoveFromGroupAsync(connectionId, $"lobby_{lobby.Id}");

            Console.WriteLine($"[SignalR] {connectionId} left lobby {lobbyId}. Remaining: {lobby.MemberCount}/{lobby.MaxPlayers}");

            if (lobby.MemberCount == 0)
            {
                // ✅ PlayFab 서버 종료
                if (!string.IsNullOrEmpty(lobby.PlayFabSessionId))
                {
                    try
                    {
                        await _playFabService.ShutdownServer(lobby.PlayFabSessionId);
                        Console.WriteLine($"[PlayFab] Server shutdown requested: {lobby.PlayFabSessionId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[PlayFab] Failed to shutdown server: {ex.Message}");
                    }
                }

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
