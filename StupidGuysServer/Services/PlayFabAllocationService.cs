using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace StupidGuysServer.Services
{
    public class PlayFabAllocationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _titleId;
        private readonly string _secretKey;
        private readonly string _buildId;

        public PlayFabAllocationService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClient = httpClientFactory.CreateClient();
            
            _titleId = config["PlayFab:TitleId"] ?? throw new ArgumentNullException("PlayFab:TitleId not configured");
            _secretKey = config["PlayFab:SecretKey"] ?? throw new ArgumentNullException("PlayFab:SecretKey not configured");
            _buildId = config["PlayFab:BuildId"] ?? throw new ArgumentNullException("PlayFab:BuildId not configured");
            
            _httpClient.BaseAddress = new Uri($"https://{_titleId}.playfabapi.com/");
            _httpClient.DefaultRequestHeaders.Add("X-SecretKey", _secretKey);
        }

        public async Task<ServerAllocationResponse> RequestServer(string sessionId)
        {
            var request = new
            {
                BuildId = _buildId,
                SessionId = sessionId,
                PreferredRegions = new[] { "EastUs", "WestUs" } // 원하는 지역 설정
            };

            Console.WriteLine($"[PlayFab] Requesting server allocation for session: {sessionId}");

            var response = await _httpClient.PostAsJsonAsync("MultiplayerServer/RequestMultiplayerServer", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"PlayFab server allocation failed: {response.StatusCode} - {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<PlayFabResponse<ServerAllocationResponse>>();
            
            if (result?.data == null)
            {
                throw new Exception("PlayFab returned null data");
            }

            Console.WriteLine($"[PlayFab] Server allocated: {result.data.IPV4Address}:{result.data.Ports[0].Num}");
            Console.WriteLine($"[PlayFab] SessionId: {result.data.SessionId}");

            return result.data;
        }

        public async Task<bool> ShutdownServer(string sessionId)
        {
            try
            {
                Console.WriteLine($"[PlayFab] Requesting server shutdown for session: {sessionId}");

                var request = new { SessionId = sessionId };
                var response = await _httpClient.PostAsJsonAsync("MultiplayerServer/ShutdownMultiplayerServer", request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[PlayFab] Server shutdown requested successfully: {sessionId}");
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[PlayFab] Failed to shutdown server: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PlayFab] Exception during shutdown: {ex.Message}");
                return false;
            }
        }
    }

    // Response Models
    public class PlayFabResponse<T>
    {
        [JsonPropertyName("code")]
        public int code { get; set; }

        [JsonPropertyName("status")]
        public string status { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public T? data { get; set; }
    }

    public class ServerAllocationResponse
    {
        [JsonPropertyName("SessionId")]
        public string SessionId { get; set; } = string.Empty;

        [JsonPropertyName("IPV4Address")]
        public string IPV4Address { get; set; } = string.Empty;

        [JsonPropertyName("Ports")]
        public ServerPort[] Ports { get; set; } = Array.Empty<ServerPort>();

        [JsonPropertyName("Region")]
        public string Region { get; set; } = string.Empty;
    }

    public class ServerPort
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Num")]
        public int Num { get; set; }

        [JsonPropertyName("Protocol")]
        public string Protocol { get; set; } = string.Empty;
    }
}
