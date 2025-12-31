using StupidGuysServer.Models;
using System.Collections.Concurrent;
using System.Linq;

namespace StupidGuysServer.Services
{

    /// <summary>
    /// 로비 관리 비즈니스 로직
    /// SignalR Hub에서 사용
    /// </summary>
    public class LobbiesManager
    {
        private readonly ConcurrentDictionary<int, Lobby> _lobbies = new();
        private int _nextLobbyId = 1;
        private readonly object _idLock = new object();

        /// <summary>
        /// 참가 가능한 로비 찾기
        /// </summary>
        public Lobby? FindAvailableLobby()
        {
            return _lobbies.Values.FirstOrDefault(lobby => !lobby.IsFull);
        }

        /// <summary>
        /// 새 로비 생성
        /// </summary>
        public Lobby CreateLobby(int maxPlayers)
        {
            int lobbyId;
            lock (_idLock)
            {
                lobbyId = _nextLobbyId++;
            }

            var lobby = new Lobby(lobbyId, maxPlayers);
            _lobbies[lobbyId] = lobby;

            return lobby;
        }

        /// <summary>
        /// 로비 ID로 조회
        /// </summary>
        public Lobby? GetLobby(int lobbyId)
        {
            _lobbies.TryGetValue(lobbyId, out var lobby);
            return lobby;
        }

        /// <summary>
        /// 플레이어를 모든 로비에서 제거
        /// SignalR 연결 해제 시 사용
        /// </summary>
        public Lobby? RemovePlayerFromAllLobbies(string connectionId)
        {
            foreach (var lobby in _lobbies.Values)
            {
                if (lobby.TryRemoveMember(connectionId, out int remainCount))
                {
                    // 로비가 비었으면 삭제
                    if (remainCount == 0)
                    {
                        _lobbies.TryRemove(lobby.Id, out _);
                    }

                    return lobby;
                }
            }

            return null;
        }
    }
}