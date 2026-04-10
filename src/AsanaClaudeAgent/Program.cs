using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Hosting;
using AsanaClaudeAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration sections
builder.Services.Configure<AsanaSettings>(builder.Configuration.GetSection("Asana"));
builder.Services.Configure<ClaudeSettings>(builder.Configuration.GetSection("Claude"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

// Override RunOnce from --once CLI arg
if (args.Contains("--once", StringComparer.OrdinalIgnoreCase))
{
    builder.Services.PostConfigure<AppSettings>(s => s.RunOnce = true);
}

// Override token from env var if not set in config
builder.Services.PostConfigure<AsanaSettings>(s =>
{
    var envToken = Environment.GetEnvironmentVariable("ASANA_TOKEN");
    if (!string.IsNullOrEmpty(envToken) && string.IsNullOrEmpty(s.Token))
        s.Token = envToken;
});

// Register HttpClient for Asana
builder.Services.AddHttpClient<IAsanaService, AsanaService>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<AsanaSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.Token}");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register services
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IClaudeProcessRunner, ClaudeProcessRunner>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddSingleton<IClassifierService, ClassifierService>();
builder.Services.AddSingleton<IWorkerService, WorkerService>();
builder.Services.AddSingleton<ITaskOrchestrator, TaskOrchestrator>();

// Register hosted service
builder.Services.AddHostedService<OrchestratorHostedService>();

var host = builder.Build();
await host.RunAsync();
