using StupidGuysServer.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace StupidGuysServer.Services
{
    public class LobbiesManager
    {
        private readonly ConcurrentDictionary<int, Lobby> _lobbies = new();
        private int _nextLobbyId = 1;
        private readonly object _idLock = new object();


        public Lobby? FindLobby()
        {
            return _lobbies.Values.FirstOrDefault(lobby => !lobby.IsFull);
        }

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

        public bool RemoveLobby(int lobbyId)
        {
            var lobby = GetLobby(lobbyId);
            if (lobby != null)
            {
                return _lobbies.TryRemove(lobbyId, out lobby);
            }
            return false;
        }

        public Lobby? GetLobby(int lobbyId)
        {
            _lobbies.TryGetValue(lobbyId, out var lobby);
            return lobby;
        }

        public Lobby? RemovePlayerFromAllLobbies(string connectionId)
        {
            foreach (var lobby in _lobbies.Values)
            {
                if (lobby.RemoveMember(connectionId))
                {
                    if (lobby.MemberCount == 0)
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