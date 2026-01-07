using Microsoft.AspNetCore.SignalR;
using StupidGuysServer.Models;
using StupidGuysServer.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

public class MatchmakingHub : Hub
{
    private readonly LobbiesManager _lobbiesManager;
    private readonly EdgeGapService _edgeGapService;

    public MatchmakingHub(LobbiesManager lobbiesManager, EdgeGapService edgeGapService)
    {
        _lobbiesManager = lobbiesManager;
        _edgeGapService = edgeGapService;
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

            // EdgeGap 서버 삭제 (로비가 비었을 때)
            if (lobby.MemberCount == 0 && !string.IsNullOrEmpty(lobby.EdgeGapRequestId))
            {
                try
                {
                    await _edgeGapService.DeleteDeployment(lobby.EdgeGapRequestId);
                    Console.WriteLine($"[EdgeGap] Server deleted on disconnect: {lobby.EdgeGapRequestId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[EdgeGap] Failed to delete server on disconnect: {ex.Message}");
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
            Console.WriteLine($"Created new lobby {lobby.Id}");

            // ✅ EdgeGap 서버 할당
            try
            {
                Console.WriteLine($"[EdgeGap] Requesting server allocation...");

                // 빈 IP 리스트 - EdgeGap이 자동으로 최적 위치 선택
                var playerIPs = new string[] { };

                var deployment = await _edgeGapService.CreateDeployment(playerIPs);

                lobby.EdgeGapRequestId = deployment.request_id;
                Console.WriteLine($"[EdgeGap] Deployment created: {deployment.request_id}");
                Console.WriteLine($"[EdgeGap] Waiting for server to be ready...");

                // Status 체크 (최대 30초 대기)
                int maxRetries = 30;
                int retryCount = 0;
                bool isReady = false;

                while (retryCount < maxRetries && !isReady)
                {
                    await Task.Delay(2000); // 2초 대기
                    
                    var status = await _edgeGapService.GetDeploymentStatus(deployment.request_id);
                    
                    Console.WriteLine($"[EdgeGap] Status: {status.current_status} (Attempt {retryCount + 1}/{maxRetries})");
                    Console.WriteLine($"[EdgeGap] DEBUG - ready: {status.ready}, ports: {status.ports?.Count ?? 0}, public_ip: {status.public_ip}");
                    
                    if (status.ready && status.ports != null && status.ports.Count > 0)
                    {
                        lobby.GameServerIP = status.public_ip ?? deployment.public_ip;
                        
                        // Dictionary의 첫 번째 포트 가져오기
                        var firstPort = status.ports.Values.First();
                        lobby.GameServerPort = firstPort.external;
                        
                        isReady = true;
                        break;
                    }
                    
                    retryCount++;
                }

                if (!isReady)
                {
                    throw new Exception("Server deployment timeout - not ready after 30 seconds");
                }
                
                lobby.IsGameServerAllocated = true;

                Console.WriteLine($"[EdgeGap] ✅ Server allocated successfully!");
                Console.WriteLine($"[EdgeGap] Request ID: {deployment.request_id}");
                Console.WriteLine($"[EdgeGap] IP: {lobby.GameServerIP}");
                Console.WriteLine($"[EdgeGap] Port: {lobby.GameServerPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EdgeGap] ❌ Failed to allocate server: {ex.Message}");
                Console.WriteLine($"[EdgeGap] Falling back to local server...");

                // Fallback: 로컬 서버 사용
                lobby.GameServerIP = "127.0.0.1";
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
                // ✅ EdgeGap 서버 삭제
                if (!string.IsNullOrEmpty(lobby.EdgeGapRequestId))
                {
                    try
                    {
                        await _edgeGapService.DeleteDeployment(lobby.EdgeGapRequestId);
                        Console.WriteLine($"[EdgeGap] Server deleted: {lobby.EdgeGapRequestId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EdgeGap] Failed to delete server: {ex.Message}");
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
