using Hangfire;
using Hangfire.PostgreSql;
using LexFlow.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options
        .UseNpgsqlConnection(builder.Configuration.GetConnectionString("LexFlowDatabase"))));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = ["default"];
});

// Job registry — empty. Recurring jobs (OCR, indexing, reminders, dunning, etc.)
// are registered here via RecurringJob.AddOrUpdate<T>(...) as modules ship.

var host = builder.Build();
host.Run();
