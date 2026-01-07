using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

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

            _apiToken = config["EdgeGap:ApiToken"];
            _appName = config["EdgeGap:AppName"];
            _versionName = config["EdgeGap:VersionName"];

            _httpClient.DefaultRequestHeaders.Add("Authorization", _apiToken);
        }

        public async Task<DeploymentResponse> CreateDeployment(string[] playerIPs)
        {
            var request = new
            {
                app_name = _appName,
                version_name = _versionName,
                ip_list = playerIPs,
                webhook_url = "" // 선택사항
            };

            var response = await _httpClient.PostAsJsonAsync("deploy", request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<DeploymentResponse>();
        }

        public async Task<bool> DeleteDeployment(string requestId)
        {
            var response = await _httpClient.DeleteAsync($"stop/{requestId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<DeploymentStatus> GetDeploymentStatus(string requestId)
        {
            var response = await _httpClient.GetAsync($"status/{requestId}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<DeploymentStatus>();
        }
    }

    // Response Models
    public class DeploymentResponse
    {
        public string request_id { get; set; }
        public string fqdn { get; set; } // 서버 도메인
        public PortMapping[] ports { get; set; }
        public string public_ip { get; set; }
    }

    public class PortMapping
    {
        public int external { get; set; } // 외부 포트
        public int internal_ { get; set; } // 내부 포트
        public string protocol { get; set; }
    }

    public class DeploymentStatus
    {
        public string request_id { get; set; }
        public string current_status { get; set; } // "Running", "Deploying", etc
        public bool ready { get; set; }
        public string fqdn { get; set; }
        public PortMapping[] ports { get; set; }
    }
}