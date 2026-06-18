namespace Prismedia.Infrastructure.Database;

/// <summary>
/// Infrastructure settings for Postgres backup and restore operations.
/// </summary>
public sealed record DatabaseBackupOptions(
    string ConnectionString,
    string BackupDirectory,
    string RestoreRequestPath,
    string PgDumpPath,
    string PgRestorePath,
    int AutomaticRetentionDays,
    TimeSpan AutomaticInterval);
