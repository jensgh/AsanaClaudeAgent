using AsanaClaudeAgent.Configuration;
using AsanaClaudeAgent.Hosting;
using AsanaClaudeAgent.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<AsanaSettings>(builder.Configuration.GetSection("Asana"));
builder.Services.Configure<ClaudeSettings>(builder.Configuration.GetSection("Claude"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("App"));

if (args.Contains("--once", StringComparer.OrdinalIgnoreCase))
{
    builder.Services.PostConfigure<AppSettings>(s => s.RunOnce = true);
}

builder.Services.PostConfigure<AsanaSettings>(s =>
{
    var envToken = Environment.GetEnvironmentVariable("ASANA_TOKEN");
    if (!string.IsNullOrEmpty(envToken) && string.IsNullOrEmpty(s.Token))
    {
        s.Token = envToken;
    }
});

builder.Services.AddHttpClient<IAsanaService, AsanaService>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<AsanaSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.Token}");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IClaudeProcessRunner, ClaudeProcessRunner>();
builder.Services.AddSingleton<IGitService, GitService>();
builder.Services.AddSingleton<IClassifierService, ClassifierService>();
builder.Services.AddSingleton<IWorkerService, WorkerService>();
builder.Services.AddSingleton<ITaskOrchestrator, TaskOrchestrator>();
builder.Services.AddHostedService<OrchestratorHostedService>();

var host = builder.Build();

var asanaSettings = host.Services.GetRequiredService<IOptions<AsanaSettings>>().Value;
var claudeSettings = host.Services.GetRequiredService<IOptions<ClaudeSettings>>().Value;
var appSettings = host.Services.GetRequiredService<IOptions<AppSettings>>().Value;
StartupValidator.Validate(asanaSettings, claudeSettings, appSettings);

await host.RunAsync();
