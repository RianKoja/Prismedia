using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prismedia.Application.Backups;
using Prismedia.Contracts.Settings;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Database;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Tests;

public sealed class DatabaseBackupServiceTests : IDisposable {
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"prismedia-backups-{Guid.NewGuid():N}");

    [Fact]
    public async Task ManualBackupCreatesPermanentCompletedDump() {
        await using var db = CreateContext();
        var process = new DumpProcessExecutor();
        var service = CreateService(db, process);

        var backup = await service.CreateManualBackupAsync(CancellationToken.None);

        Assert.Equal(DatabaseBackupStatus.Completed, backup.Status);
        Assert.True(backup.IsManual);
        Assert.Null(backup.ExpiresAt);
        Assert.True(backup.SizeBytes > 0);
        Assert.True(File.Exists(backup.BackupPath));
        Assert.Equal("pg_dump", process.Calls.Single().FileName);
    }

    [Fact]
    public async Task AutomaticRetentionDeletesOnlyExpiredAutomaticBackups() {
        await using var db = CreateContext();
        var backupDir = Path.Combine(_tempDir, "database");
        Directory.CreateDirectory(backupDir);
        var expiredPath = Path.Combine(backupDir, "expired.dump");
        var manualPath = Path.Combine(backupDir, "manual.dump");
        await File.WriteAllTextAsync(expiredPath, "expired");
        await File.WriteAllTextAsync(manualPath, "manual");
        var now = DateTimeOffset.UtcNow;

        db.DatabaseBackups.AddRange(
            new DatabaseBackupRow {
                Id = Guid.NewGuid(),
                BackupPath = expiredPath,
                Status = DatabaseBackupStatus.Completed,
                CreatedAt = now.AddDays(-8),
                CompletedAt = now.AddDays(-8),
                ExpiresAt = now.AddDays(-1)
            },
            new DatabaseBackupRow {
                Id = Guid.NewGuid(),
                BackupPath = manualPath,
                Status = DatabaseBackupStatus.Completed,
                IsManual = true,
                CreatedAt = now.AddDays(-30),
                CompletedAt = now.AddDays(-30)
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, new DumpProcessExecutor());
        var pruned = await service.PruneExpiredAutomaticBackupsAsync(CancellationToken.None);

        Assert.Equal(1, pruned);
        Assert.False(File.Exists(expiredPath));
        Assert.True(File.Exists(manualPath));
        Assert.Single(await db.DatabaseBackups.ToListAsync());
    }

    [Fact]
    public async Task RestoreRequiresConfirmationAndStagesMarker() {
        await using var db = CreateContext();
        var backupDir = Path.Combine(_tempDir, "database");
        Directory.CreateDirectory(backupDir);
        var path = Path.Combine(backupDir, "manual.dump");
        await File.WriteAllTextAsync(path, "backup");
        var backupId = Guid.NewGuid();
        db.DatabaseBackups.Add(new DatabaseBackupRow {
            Id = backupId,
            BackupPath = path,
            Status = DatabaseBackupStatus.Completed,
            IsManual = true,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new DumpProcessExecutor());
        var rejected = await Assert.ThrowsAsync<DatabaseBackupException>(() =>
            service.ScheduleRestoreAsync(backupId, "please", CancellationToken.None));
        Assert.Equal(ApiProblemCodes.DatabaseRestoreInvalid, rejected.ProblemCode);

        var scheduled = await service.ScheduleRestoreAsync(
            backupId,
            DatabaseRestoreConfirmation.Text,
            CancellationToken.None);

        Assert.Equal(backupId, scheduled.BackupId);
        Assert.True(scheduled.RestartScheduled);
        Assert.True(await service.HasPendingRestoreAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PendingRestoreRunsPgRestoreAndClearsMarker() {
        await using var db = CreateContext();
        var backupDir = Path.Combine(_tempDir, "database");
        Directory.CreateDirectory(backupDir);
        var path = Path.Combine(backupDir, "manual.dump");
        await File.WriteAllTextAsync(path, "backup");
        var backupId = Guid.NewGuid();
        db.DatabaseBackups.Add(new DatabaseBackupRow {
            Id = backupId,
            BackupPath = path,
            Status = DatabaseBackupStatus.Completed,
            IsManual = true,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var process = new DumpProcessExecutor();
        var service = CreateService(db, process);
        await service.ScheduleRestoreAsync(backupId, DatabaseRestoreConfirmation.Text, CancellationToken.None);

        var restored = await service.RunPendingRestoreAsync(CancellationToken.None);

        Assert.True(restored);
        Assert.Equal("pg_restore", Assert.Single(process.Calls).FileName);
        Assert.False(await service.HasPendingRestoreAsync(CancellationToken.None));
    }

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private DatabaseBackupService CreateService(PrismediaDbContext db, ProcessExecutor process) =>
        new(
            db,
            process,
            new DatabaseBackupOptions(
                "Host=localhost;Port=5432;Database=prismedia;Username=prismedia;Password=prismedia",
                Path.Combine(_tempDir, "database"),
                Path.Combine(_tempDir, "database", "restore-request.json"),
                "pg_dump",
                "pg_restore",
                7,
                TimeSpan.FromDays(1)),
            NullLogger<DatabaseBackupService>.Instance);

    private static PrismediaDbContext CreateContext() {
        var options = new DbContextOptionsBuilder<PrismediaDbContext>()
            .UseInMemoryDatabase($"database-backups-{Guid.NewGuid():N}")
            .Options;
        return new PrismediaDbContext(options);
    }

    private sealed class DumpProcessExecutor : ProcessExecutor {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public override async Task<ProcessExecutionResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment,
            CancellationToken cancellationToken,
            bool lowPriority = false) {
            Calls.Add((fileName, arguments.ToArray()));

            if (fileName == "pg_dump") {
                var fileIndex = Array.IndexOf(arguments.ToArray(), "--file");
                if (fileIndex >= 0 && fileIndex + 1 < arguments.Count) {
                    Directory.CreateDirectory(Path.GetDirectoryName(arguments[fileIndex + 1])!);
                    await File.WriteAllTextAsync(arguments[fileIndex + 1], "backup", cancellationToken);
                }
            }

            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }
}
