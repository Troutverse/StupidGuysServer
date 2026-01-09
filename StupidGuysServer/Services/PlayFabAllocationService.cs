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
            Console.WriteLine($"[PlayFab] BuildId: {_buildId}");
            Console.WriteLine($"[PlayFab] TitleId: {_titleId}");

            var request = new RequestMultiplayerServerRequest
            {
                BuildId = _buildId,
                SessionId = sessionId,
                PreferredRegions = new System.Collections.Generic.List<string> { "KoreaCentral" }
            };

            Console.WriteLine($"[PlayFab] Request - BuildId: {request.BuildId}");
            Console.WriteLine($"[PlayFab] Request - SessionId: {request.SessionId}");
            Console.WriteLine($"[PlayFab] Request - Regions: {string.Join(", ", request.PreferredRegions)}");

            var result = await PlayFabMultiplayerAPI.RequestMultiplayerServerAsync(request);

            if (result.Error != null)
            {
                Console.WriteLine($"[PlayFab] Error Code: {result.Error.Error}");
                Console.WriteLine($"[PlayFab] Error Message: {result.Error.ErrorMessage}");
                if (result.Error.ErrorDetails != null)
                {
                    foreach (var detail in result.Error.ErrorDetails)
                    {
                        Console.WriteLine($"[PlayFab] Error Detail - {detail.Key}: {string.Join(", ", detail.Value)}");
                    }
                }
                throw new Exception($"PlayFab Error: {result.Error.ErrorMessage}");
            }

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

    public class ServerAllocationResponse
    {
        public string SessionId { get; set; } = string.Empty;
        public string IPV4Address { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Region { get; set; } = string.Empty;
    }
}