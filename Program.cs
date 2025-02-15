﻿﻿﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Voxta.Client;
using Voxta.Providers.Host;
using Voxta.VoxtaMCPBridge;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        // Dependency Injection
        var services = new ServiceCollection();

        // Configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", true)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Logging
        await using var log = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Filter.ByExcluding(logEvent => logEvent.Exception?.GetType().IsSubclassOf(typeof(OperationCanceledException)) ?? false)
            .CreateLogger();
        services.AddLogging(builder =>
        {
            builder.AddSerilog(log);
        });

        // Dependencies
        services.AddHttpClient();

        // Voxta Providers
        services.AddVoxtaProvider(builder =>
        {
            builder.AddProvider<MCPBridgeProvider>();
        });

        // Build the application
        var sp = services.BuildServiceProvider();
        var runtime = sp.GetRequiredService<IProviderAppHandler>();

        // Run the application
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        await runtime.RunAsync(cts.Token);
    }
}
