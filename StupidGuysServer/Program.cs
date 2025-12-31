using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StupidGuysServer.Services;

var builder = WebApplication.CreateBuilder(args);

// SignalR 추가
builder.Services.AddSignalR();

// LobbiesManager를 Singleton으로 등록
builder.Services.AddSingleton<LobbiesManager>();

// CORS 설정 - 모든 도메인 허용 (Render.com 배포용)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)  // 모든 origin 허용
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // SignalR은 Credentials 필요
    });
});

var app = builder.Build();

// CORS 적용
app.UseCors("AllowAll");

// SignalR Hub 엔드포인트 매핑
app.MapHub<MatchmakingHub>("/matchmaking");

// Render.com은 PORT 환경 변수 사용, 기본값 10000
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
var url = $"http://0.0.0.0:{port}";

Console.WriteLine($"SignalR Server starting at {url}/matchmaking");
Console.WriteLine($"CORS: AllowAll enabled");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");

app.Run(url);