using PlayFab;
using PlayFab.MultiplayerModels;
using System.Threading.Tasks;

namespace StupidGuysServer.Services
{
    public class PlayFabAllocationService
    {
        private readonly string _titleId;
        private readonly string _secretKey;
        private readonly string _buildId;

        public PlayFabAllocationService(IConfiguration config)
        {
            _titleId = config["PlayFab:TitleId"] ?? throw new ArgumentNullException("PlayFab:TitleId not configured");
            _secretKey = config["PlayFab:SecretKey"] ?? throw new ArgumentNullException("PlayFab:SecretKey not configured");
            _buildId = config["PlayFab:BuildId"] ?? throw new ArgumentNullException("PlayFab:BuildId not configured");

            PlayFabSettings.staticSettings.TitleId = _titleId;
            PlayFabSettings.staticSettings.DeveloperSecretKey = _secretKey;
        }

        public async Task<ServerAllocationResponse> RequestServer(string sessionId)
        {
            // EntityToken 먼저 획득
            if (string.IsNullOrEmpty(PlayFabSettings.staticSettings.DeveloperSecretKey))
            {
                throw new Exception("DeveloperSecretKey not set");
            }

            var authRequest = new PlayFab.AuthenticationModels.GetEntityTokenRequest();
            var authResult = await PlayFab.PlayFabAuthenticationAPI.GetEntityTokenAsync(authRequest);

            if (authResult.Error != null)
            {
                throw new Exception($"Auth failed: {authResult.Error.ErrorMessage}");
            }

            Console.WriteLine($"[PlayFab] EntityToken acquired");
            Console.WriteLine($"[PlayFab] Requesting server allocation for session: {sessionId}");

            var request = new RequestMultiplayerServerRequest
            {
                BuildId = _buildId,
                SessionId = sessionId,
                PreferredRegions = new System.Collections.Generic.List<string> { "EastUs", "WestUs", "KoreaCentral" }
            };

            var result = await PlayFabMultiplayerAPI.RequestMultiplayerServerAsync(request);

            if (result.Error != null)
            {
                throw new Exception($"PlayFab Error: {result.Error.ErrorMessage}");
            }

            Console.WriteLine($"[PlayFab] ✅ Server allocated successfully!");
            Console.WriteLine($"[PlayFab] SessionId: {result.Result.SessionId}");
            Console.WriteLine($"[PlayFab] IP: {result.Result.IPV4Address}");
            Console.WriteLine($"[PlayFab] Port: {result.Result.Ports[0].Num}");

            return new ServerAllocationResponse
            {
                SessionId = result.Result.SessionId,
                IPV4Address = result.Result.IPV4Address,
                Port = result.Result.Ports[0].Num,
                Region = result.Result.Region
            };
        }

        public async Task<bool> ShutdownServer(string sessionId)
        {
            try
            {
                Console.WriteLine($"[PlayFab] Requesting server shutdown for session: {sessionId}");

                var request = new ShutdownMultiplayerServerRequest
                {
                    SessionId = sessionId,
                };

                var result = await PlayFabMultiplayerAPI.ShutdownMultiplayerServerAsync(request);

                if (result.Error != null)
                {
                    Console.WriteLine($"[PlayFab] Failed to shutdown server: {result.Error.ErrorMessage}");
                    return false;
                }

                Console.WriteLine($"[PlayFab] Server shutdown requested successfully: {sessionId}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayFab] Exception during shutdown: {ex.Message}");
                return false;
            }
        }
    }

    // Response Model
    public class ServerAllocationResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string IPV4Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Region { get; set; } = string.Empty;
    }
}