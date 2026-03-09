using App.Agent.Daemon;
using DotNetEnv;

// Load .env file
Env.Load();

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSingleton<SystemInfoService>();
builder.Services.AddSingleton<SystemInfoReporter>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
