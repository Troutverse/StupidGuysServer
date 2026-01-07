using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace StupidGuysServer.Services
{
    public class EdgeGapService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiToken;
        private readonly string _appName;
        private readonly string _versionName;

        public EdgeGapService(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.BaseAddress = new Uri("https://api.edgegap.com/v1/");
            
            _apiToken = config["EdgeGap:ApiToken"] ?? throw new ArgumentNullException("EdgeGap:ApiToken not configured");
            _appName = config["EdgeGap:AppName"] ?? throw new ArgumentNullException("EdgeGap:AppName not configured");
            _versionName = config["EdgeGap:VersionName"] ?? throw new ArgumentNullException("EdgeGap:VersionName not configured");
            
            // EdgeGap expects "Authorization: token <token_value>"
            // Remove "token_" prefix if present
            var tokenValue = _apiToken.StartsWith("token_") ? _apiToken.Substring(6) : _apiToken;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {tokenValue}");
        }

        public async Task<DeploymentResponse> CreateDeployment(string[] playerIPs)
        {
            var request = new
            {
                app_name = _appName,
                version_name = _versionName,
                // 빈 IP 리스트 대신 고정 위치 지정
                location = new
                {
                    city = "Seoul",
                    country = "South Korea",
                    continent = "Asia",
                    latitude = 37.5665,
                    longitude = 126.9780
                }
            };

            Console.WriteLine($"[EdgeGap] Creating deployment for app: {_appName}, version: {_versionName}");
            
            // DEBUG: 실제 전송되는 Authorization 헤더 확인
            var authHeader = _httpClient.DefaultRequestHeaders.GetValues("Authorization").FirstOrDefault();
            Console.WriteLine($"[EdgeGap] DEBUG - Authorization header: {authHeader}");
            Console.WriteLine($"[EdgeGap] DEBUG - Request body: {System.Text.Json.JsonSerializer.Serialize(request)}");

            var response = await _httpClient.PostAsJsonAsync("deploy", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"EdgeGap deployment failed: {response.StatusCode} - {error}");
            }
            
            var deployment = await response.Content.ReadFromJsonAsync<DeploymentResponse>();
            Console.WriteLine($"[EdgeGap] Deployment created: {deployment?.request_id}");
            
            return deployment!;
        }

        public async Task<bool> DeleteDeployment(string requestId)
        {
            Console.WriteLine($"[EdgeGap] Deleting deployment: {requestId}");
            
            var response = await _httpClient.DeleteAsync($"stop/{requestId}");
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EdgeGap] Deployment deleted successfully: {requestId}");
                return true;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[EdgeGap] Failed to delete deployment: {error}");
                return false;
            }
        }

        public async Task<DeploymentStatus> GetDeploymentStatus(string requestId)
        {
            var response = await _httpClient.GetAsync($"status/{requestId}");
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get deployment status: {response.StatusCode} - {error}");
            }
            
            return (await response.Content.ReadFromJsonAsync<DeploymentStatus>())!;
        }
    }

    // Response Models
    public class DeploymentResponse
    {
        [JsonPropertyName("request_id")]
        public string request_id { get; set; } = string.Empty;
        
        [JsonPropertyName("fqdn")]
        public string fqdn { get; set; } = string.Empty; // 서버 도메인
        
        [JsonPropertyName("ports")]
        public PortMapping[] ports { get; set; } = Array.Empty<PortMapping>();
        
        [JsonPropertyName("public_ip")]
        public string public_ip { get; set; } = string.Empty;
    }

    public class PortMapping
    {
        [JsonPropertyName("external")]
        public int external { get; set; } // 외부 포트
        
        [JsonPropertyName("internal")]
        public int internal_ { get; set; } // 내부 포트
        
        [JsonPropertyName("protocol")]
        public string protocol { get; set; } = string.Empty;
    }

    public class DeploymentStatus
    {
        [JsonPropertyName("request_id")]
        public string request_id { get; set; } = string.Empty;
        
        [JsonPropertyName("current_status")]
        public string current_status { get; set; } = string.Empty; // "Running", "Deploying", etc
        
        [JsonPropertyName("ready")]
        public bool ready { get; set; }
        
        [JsonPropertyName("fqdn")]
        public string fqdn { get; set; } = string.Empty;
        
        [JsonPropertyName("ports")]
        public PortMapping[] ports { get; set; } = Array.Empty<PortMapping>();
    }
}
