using Prismedia.Application;
using Prismedia.Infrastructure;
using Prismedia.Infrastructure.Database;
using Prismedia.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPrismediaApplication();
builder.Services.AddPrismediaWorkerApplication();
builder.Services.AddPrismediaInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

var host = builder.Build();

// The API owns destructive restore startup and schema application. The worker waits for any staged
// restore to finish, then waits for the database to be reachable
// and migrated so it neither races the API to migrate a fresh database nor terminates when the
// database is not yet accepting connections on first boot.
await DatabaseRestoreRunner.WaitForPendingRestoreToClearAsync(host.Services, builder.Configuration);
await PrismediaMigrationRunner.WaitForDatabaseReadyAsync(host.Services, builder.Configuration);
host.Run();
