using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Elastic.Clients.Elasticsearch;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Caching;
using LexFlow.Infrastructure.Persistence;
using LexFlow.Infrastructure.Search;
using LexFlow.Infrastructure.Secrets;
using LexFlow.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LexFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LexFlowDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("LexFlowDatabase")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LexFlowDbContext>());

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();
            return ConnectionMultiplexer.Connect(options.ConnectionString);
        });

        services.Configure<ElasticsearchOptions>(configuration.GetSection(ElasticsearchOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(ElasticsearchOptions.SectionName).Get<ElasticsearchOptions>()
                          ?? new ElasticsearchOptions();
            var settings = new ElasticsearchClientSettings(new Uri(options.Uri));
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                settings = settings.Authentication(new Elastic.Transport.ApiKey(options.ApiKey));
            }

            return new ElasticsearchClient(settings);
        });

        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(BlobStorageOptions.SectionName).Get<BlobStorageOptions>()
                          ?? new BlobStorageOptions();
            return new BlobServiceClient(options.ConnectionString);
        });

        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));
        var vaultUri = configuration.GetSection(KeyVaultOptions.SectionName)[nameof(KeyVaultOptions.VaultUri)];
        if (!string.IsNullOrWhiteSpace(vaultUri))
        {
            services.AddSingleton(new SecretClient(new Uri(vaultUri), new DefaultAzureCredential()));
        }

        return services;
    }
}
