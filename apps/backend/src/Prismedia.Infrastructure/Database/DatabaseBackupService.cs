using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Prismedia.Application.Backups;
using Prismedia.Contracts.Settings;
using Prismedia.Contracts.System;
using Prismedia.Domain.Entities;
using Prismedia.Infrastructure.Persistence;
using Prismedia.Infrastructure.Persistence.Entities;
using Prismedia.Infrastructure.Processes;

namespace Prismedia.Infrastructure.Database;

/// <summary>
/// Postgres-backed implementation of Prismedia database backups.
/// </summary>
public sealed class DatabaseBackupService(
    PrismediaDbContext db,
    ProcessExecutor processes,
    DatabaseBackupOptions options,
    ILogger<DatabaseBackupService> logger,
    TimeProvider? timeProvider = null) : IDatabaseBackupService {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim BackupGate = new(1, 1);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DatabaseBackupListResponse> ListAsync(CancellationToken cancellationToken) {
        await PruneExpiredAutomaticBackupsAsync(cancellationToken);

        var rows = await db.DatabaseBackups
            .AsNoTracking()
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(cancellationToken);

        return new DatabaseBackupListResponse(
            rows.Select(ToDto).ToList(),
            NextAutomaticBackupAt(rows),
            options.BackupDirectory,
            options.AutomaticRetentionDays,
            DatabaseRestoreConfirmation.Text);
    }

    public Task<DatabaseBackupDto> CreateManualBackupAsync(CancellationToken cancellationToken) =>
        CreateBackupAsync(isManual: true, cancellationToken);

    public Task<DatabaseBackupDto> CreateAutomaticBackupAsync(CancellationToken cancellationToken) =>
        CreateBackupAsync(isManual: false, cancellationToken);

    public async Task<bool> IsAutomaticBackupDueAsync(CancellationToken cancellationToken) {
        var running = await db.DatabaseBackups
            .AsNoTracking()
            .AnyAsync(row => !row.IsManual && row.Status == DatabaseBackupStatus.Running, cancellationToken);
        if (running) {
            return false;
        }

        var lastCompleted = await db.DatabaseBackups
            .AsNoTracking()
            .Where(row => !row.IsManual && row.Status == DatabaseBackupStatus.Completed)
            .OrderByDescending(row => row.CompletedAt ?? row.CreatedAt)
            .Select(row => (DateTimeOffset?)(row.CompletedAt ?? row.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return lastCompleted is null || _timeProvider.GetUtcNow() - lastCompleted >= options.AutomaticInterval;
    }

    public async Task<int> PruneExpiredAutomaticBackupsAsync(CancellationToken cancellationToken) {
        var now = _timeProvider.GetUtcNow();
        var expired = await db.DatabaseBackups
            .Where(row => !row.IsManual && row.ExpiresAt != null && row.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0) {
            return 0;
        }

        foreach (var row in expired) {
            DeleteBackupFile(row.BackupPath);
            db.DatabaseBackups.Remove(row);
        }

        await db.SaveChangesAsync(cancellationToken);
        return expired.Count;
    }

    public async Task<DatabaseRestoreScheduledResponse> ScheduleRestoreAsync(
        Guid backupId,
        string confirmationText,
        CancellationToken cancellationToken) {
        if (!string.Equals(
                confirmationText,
                DatabaseRestoreConfirmation.Text,
                StringComparison.Ordinal)) {
            throw new DatabaseBackupException(
                ApiProblemCodes.DatabaseRestoreInvalid,
                $"Type {DatabaseRestoreConfirmation.Text} to confirm database restore.");
        }

        var row = await db.DatabaseBackups
            .AsNoTracking()
            .FirstOrDefaultAsync(backup => backup.Id == backupId, cancellationToken);
        if (row is null) {
            throw new DatabaseBackupException(
                ApiProblemCodes.DatabaseBackupNotFound,
                $"Database backup '{backupId}' was not found.");
        }

        if (row.Status != DatabaseBackupStatus.Completed) {
            throw new DatabaseBackupException(
                ApiProblemCodes.DatabaseBackupInvalid,
                "Only completed backups can be restored.");
        }

        if (!File.Exists(row.BackupPath)) {
            throw new DatabaseBackupException(
                ApiProblemCodes.DatabaseBackupInvalid,
                "The selected backup file no longer exists on disk.");
        }

        var requestedAt = _timeProvider.GetUtcNow();
        var request = new DatabaseRestoreRequestFile(row.Id, row.BackupPath, requestedAt);
        Directory.CreateDirectory(Path.GetDirectoryName(options.RestoreRequestPath)!);
        await File.WriteAllTextAsync(
            options.RestoreRequestPath,
            JsonSerializer.Serialize(request, JsonOptions),
            cancellationToken);

        return new DatabaseRestoreScheduledResponse(row.Id, requestedAt, RestartScheduled: true);
    }

    public Task<bool> HasPendingRestoreAsync(CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(options.RestoreRequestPath));

    public async Task<bool> RunPendingRestoreAsync(CancellationToken cancellationToken) {
        if (!File.Exists(options.RestoreRequestPath)) {
            return false;
        }

        await BackupGate.WaitAsync(cancellationToken);
        try {
            if (!File.Exists(options.RestoreRequestPath)) {
                return false;
            }

            DatabaseRestoreRequestFile request;
            try {
                var json = await File.ReadAllTextAsync(options.RestoreRequestPath, cancellationToken);
                request = JsonSerializer.Deserialize<DatabaseRestoreRequestFile>(json, JsonOptions)
                    ?? throw new InvalidOperationException("Restore request file is empty.");
            } catch (Exception ex) {
                MoveRestoreRequestAside(ex);
                throw new DatabaseBackupException(
                    ApiProblemCodes.DatabaseRestoreInvalid,
                    "The pending restore request file could not be read.");
            }

            var backupPath = Path.GetFullPath(request.BackupPath);
            if (!IsPathUnderDirectory(backupPath, options.BackupDirectory) || !File.Exists(backupPath)) {
                MoveRestoreRequestAside();
                throw new DatabaseBackupException(
                    ApiProblemCodes.DatabaseBackupInvalid,
                    "The pending restore backup file is missing or outside the backup directory.");
            }

            logger.LogWarning("Restoring Prismedia database from {BackupPath}.", backupPath);
            NpgsqlConnection.ClearAllPools();

            ProcessExecutionResult result;
            try {
                result = await processes.RunAsync(
                    options.PgRestorePath,
                    [
                        "--clean",
                        "--if-exists",
                        "--no-owner",
                        "--no-acl",
                        "--dbname",
                        DatabaseName(),
                        backupPath
                    ],
                    BuildPostgresEnvironment(),
                    cancellationToken);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                MoveRestoreRequestAside(ex);
                throw new DatabaseBackupException(
                    ApiProblemCodes.DatabaseRestoreInvalid,
                    $"Database restore failed: {ex.Message}");
            }

            if (result.ExitCode != 0) {
                MoveRestoreRequestAside(new InvalidOperationException(result.StandardError));
                throw new DatabaseBackupException(
                    ApiProblemCodes.DatabaseRestoreInvalid,
                    $"Database restore failed: {TrimProcessError(result.StandardError)}");
            }

            File.Delete(options.RestoreRequestPath);
            logger.LogWarning("Prismedia database restore completed from {BackupPath}.", backupPath);
            return true;
        } finally {
            BackupGate.Release();
        }
    }

    private async Task<DatabaseBackupDto> CreateBackupAsync(bool isManual, CancellationToken cancellationToken) {
        await BackupGate.WaitAsync(cancellationToken);
        try {
            Directory.CreateDirectory(options.BackupDirectory);

            var now = _timeProvider.GetUtcNow();
            var id = Guid.NewGuid();
            var fileName = $"{(isManual ? "prismedia-manual" : "prismedia-auto")}-{now:yyyyMMddTHHmmssZ}-{id:N}.dump";
            var backupPath = Path.Combine(options.BackupDirectory, fileName);
            var row = new DatabaseBackupRow {
                Id = id,
                BackupPath = backupPath,
                Status = DatabaseBackupStatus.Running,
                IsManual = isManual,
                CreatedAt = now,
                ExpiresAt = isManual ? null : now.AddDays(options.AutomaticRetentionDays)
            };

            db.DatabaseBackups.Add(row);
            await db.SaveChangesAsync(cancellationToken);

            try {
                var result = await processes.RunAsync(
                    options.PgDumpPath,
                    [
                        "--format=custom",
                        "--no-owner",
                        "--no-acl",
                        "--file",
                        backupPath,
                        DatabaseName()
                    ],
                    BuildPostgresEnvironment(),
                    cancellationToken,
                    lowPriority: !isManual);

                if (result.ExitCode != 0) {
                    throw new InvalidOperationException(TrimProcessError(result.StandardError));
                }

                row.Status = DatabaseBackupStatus.Completed;
                row.CompletedAt = _timeProvider.GetUtcNow();
                row.SizeBytes = File.Exists(backupPath) ? new FileInfo(backupPath).Length : null;
                row.Error = null;
                await db.SaveChangesAsync(cancellationToken);
                return ToDto(row);
            } catch (Exception ex) when (ex is not OperationCanceledException) {
                DeleteBackupFile(backupPath);
                row.Status = DatabaseBackupStatus.Failed;
                row.CompletedAt = _timeProvider.GetUtcNow();
                row.SizeBytes = null;
                row.Error = ex.Message;
                await db.SaveChangesAsync(CancellationToken.None);
                throw new DatabaseBackupException(
                    ApiProblemCodes.DatabaseBackupInvalid,
                    $"Database backup failed: {ex.Message}");
            }
        } finally {
            BackupGate.Release();
        }
    }

    private DatabaseBackupDto ToDto(DatabaseBackupRow row) {
        var fileName = Path.GetFileName(row.BackupPath);
        var size = File.Exists(row.BackupPath)
            ? new FileInfo(row.BackupPath).Length
            : row.SizeBytes;

        return new DatabaseBackupDto(
            row.Id,
            fileName,
            row.BackupPath,
            row.Status,
            row.IsManual,
            size,
            row.CreatedAt,
            row.CompletedAt,
            row.ExpiresAt,
            row.Error);
    }

    private DateTimeOffset? NextAutomaticBackupAt(IReadOnlyList<DatabaseBackupRow> rows) {
        if (rows.Any(row => !row.IsManual && row.Status == DatabaseBackupStatus.Running)) {
            return null;
        }

        var lastCompleted = rows
            .Where(row => !row.IsManual && row.Status == DatabaseBackupStatus.Completed)
            .Select(row => (DateTimeOffset?)(row.CompletedAt ?? row.CreatedAt))
            .OrderByDescending(value => value)
            .FirstOrDefault();

        return lastCompleted is null
            ? _timeProvider.GetUtcNow()
            : lastCompleted.Value.Add(options.AutomaticInterval);
    }

    private IReadOnlyDictionary<string, string> BuildPostgresEnvironment() {
        var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        var env = new Dictionary<string, string>(StringComparer.Ordinal) {
            ["PGHOST"] = string.IsNullOrWhiteSpace(builder.Host) ? "localhost" : builder.Host,
            ["PGPORT"] = builder.Port > 0 ? builder.Port.ToString() : "5432",
            ["PGDATABASE"] = DatabaseName()
        };

        if (!string.IsNullOrWhiteSpace(builder.Username)) {
            env["PGUSER"] = builder.Username;
        }

        if (!string.IsNullOrWhiteSpace(builder.Password)) {
            env["PGPASSWORD"] = builder.Password;
        }

        return env;
    }

    private string DatabaseName() {
        var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        if (string.IsNullOrWhiteSpace(builder.Database)) {
            throw new InvalidOperationException("Database backup requires a database name.");
        }

        return builder.Database;
    }

    private void MoveRestoreRequestAside(Exception? ex = null) {
        var failedPath = $"{options.RestoreRequestPath}.failed";
        if (File.Exists(failedPath)) {
            File.Delete(failedPath);
        }

        File.Move(options.RestoreRequestPath, failedPath);
        if (ex is not null) {
            File.WriteAllText($"{failedPath}.error", ex.Message);
        }
    }

    private static bool IsPathUnderDirectory(string path, string directory) {
        var root = Path.GetFullPath(directory);
        var relative = Path.GetRelativePath(root, path);
        return relative != "." &&
               !relative.StartsWith("..", StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }

    private static void DeleteBackupFile(string path) {
        try {
            if (File.Exists(path)) {
                File.Delete(path);
            }
        } catch (IOException) {
            // Backup retention should not fail the settings page because an old file is busy.
        } catch (UnauthorizedAccessException) {
            // The row is still removed on best-effort cleanup; the file can be removed by an operator later.
        }
    }

    private static string TrimProcessError(string? standardError) {
        var message = standardError?.Trim();
        return string.IsNullOrWhiteSpace(message)
            ? "pg_dump or pg_restore exited unsuccessfully."
            : message.Length > 1_000
                ? message[..1_000]
                : message;
    }

    private sealed record DatabaseRestoreRequestFile(
        Guid BackupId,
        string BackupPath,
        DateTimeOffset RequestedAt);
}
