namespace StupidGuysServer.Models
{
    public class Lobby
    {
        private readonly object _gate = new object();
        private readonly HashSet<string> _members = new();

        public int Id { get; }
        public int MaxPlayers { get; }
        public string? GameServerIP { get; set; }
        public int GameServerPort { get; set; }
        public bool IsGameServerAllocated { get; set; }

        public string? EdgeGapRequestId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Lobby(int id, int maxPlayers)
        {
            Id = id;
            MaxPlayers = maxPlayers;
        }

        public int MemberCount
        {
            get
            {
                lock (_gate)
                {
                    return _members.Count;
                }
            }
        }

        public bool IsFull
        {
            get
            {
                lock (_gate)
                {
                    return _members.Count >= MaxPlayers;
                }
            }
        }

        public bool AddMember(string connectionId, out int remainMemberCount)
        {
            lock (_gate)
            {
                if (_members.Count >= MaxPlayers)
                {
                    remainMemberCount = 0;
                    return false;
                }

                bool added = _members.Add(connectionId);
                remainMemberCount = MaxPlayers - _members.Count;
                return added;
            }
        }

        public bool RemoveMember(string connectionId)
        {
            lock (_gate)
            {
                bool removed = _members.Remove(connectionId);
                return removed;
            }
        }
    }
}