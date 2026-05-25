using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Prismedia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RootScopedFileExclusions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_media_file_ignores",
                table: "media_file_ignores");

            migrationBuilder.AddColumn<Guid>(
                name: "library_root_id",
                table: "media_file_ignores",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "media_file_ignores",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "file");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "media_file_ignores",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.Sql(
                """
                UPDATE media_file_ignores AS ignore
                SET library_root_id = root.id,
                    path = trim(both '/' from regexp_replace(substring(ignore.path from length(root.path) + 1), '^[\\/]+', '')),
                    reason = 'excluded-from-library',
                    updated_at = NOW()
                FROM library_roots AS root
                WHERE ignore.library_root_id IS NULL
                  AND (
                    ignore.path = root.path
                    OR ignore.path LIKE root.path || '/%'
                    OR ignore.path LIKE root.path || '\\%'
                  );
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM media_file_ignores
                WHERE library_root_id IS NULL OR path = '';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "library_root_id",
                table: "media_file_ignores",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_media_file_ignores",
                table: "media_file_ignores",
                columns: new[] { "library_root_id", "path" });

            migrationBuilder.AddForeignKey(
                name: "FK_media_file_ignores_library_roots_library_root_id",
                table: "media_file_ignores",
                column: "library_root_id",
                principalTable: "library_roots",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_media_file_ignores_library_roots_library_root_id",
                table: "media_file_ignores");

            migrationBuilder.DropPrimaryKey(
                name: "PK_media_file_ignores",
                table: "media_file_ignores");

            migrationBuilder.DropColumn(
                name: "library_root_id",
                table: "media_file_ignores");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "media_file_ignores");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "media_file_ignores");

            migrationBuilder.AddPrimaryKey(
                name: "PK_media_file_ignores",
                table: "media_file_ignores",
                column: "path");
        }
    }
}
