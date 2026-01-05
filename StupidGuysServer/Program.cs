using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StupidGuysServer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

builder.Services.AddSingleton<LobbiesManager>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHub<MatchmakingHub>("/matchmaking");

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
var url = $"http://0.0.0.0:{port}";

Console.WriteLine($"SignalR Server starting at {url}/matchmaking");

app.Run(url);