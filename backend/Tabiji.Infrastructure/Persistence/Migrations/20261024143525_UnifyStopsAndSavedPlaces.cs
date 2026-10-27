using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes <c>TripDestinations</c> the one place table. A destination, a stop
    /// within one, and a legacy unfiled place all live there, distinguished by
    /// <c>Kind</c>. Every saved place is copied into it, preserving its ID and
    /// every reader-entered value.
    ///
    /// The old table is renamed to <c>SavedPlaces_Legacy</c> and kept for one
    /// release as an archive; its foreign keys are dropped so it cannot block a
    /// destination deletion. <c>MigratedFromSavedPlace</c> lets the Down
    /// migration move exactly the copied rows back, without touching a stop
    /// that was already part of the hierarchy.
    /// </summary>
    public partial class UnifyStopsAndSavedPlaces : Migration
    {
        private static readonly string[] KindIndexColumns =
            ["TripId", "Kind", "ParentDestinationId", "SortOrder"];

        private static readonly string[] RouteIndexColumns =
            ["TripId", "ParentDestinationId", "SortOrder"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripDestinations_TripId_ParentDestinationId_SortOrder",
                table: "TripDestinations");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "TripDestinations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "TripDestinations",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "TripDestinations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TripDestinations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TripDestinations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedOn",
                table: "TripDestinations",
                type: "date",
                nullable: true);

            // A rollback marker. It is not part of the EF model; it exists only
            // so the Down migration can tell a copied saved place from a stop
            // that was already in the hierarchy.
            migrationBuilder.AddColumn<bool>(
                name: "MigratedFromSavedPlace",
                table: "TripDestinations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Existing rows become destinations or stops, and keep the trip's
            // creation time as their own.
            migrationBuilder.Sql(
                """
                UPDATE d
                SET d.[Kind] = CASE WHEN d.[ParentDestinationId] IS NULL THEN 'Destination' ELSE 'Stop' END,
                    d.[CreatedAt] = t.[CreatedAt]
                FROM [TripDestinations] d
                JOIN [Trips] t ON t.[Id] = d.[TripId];
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "TripDestinations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            // Copy every saved place. A place filed under a destination
            // becomes a stop under it; one filed under a nested stop is
            // flattened to that stop's parent (the hierarchy is one level); an
            // unfiled place becomes a Kind = Place row.
            migrationBuilder.Sql(
                """
                ;WITH [Source] AS (
                    SELECT p.[Id], p.[TripId], p.[Name], p.[Latitude], p.[Longitude],
                           p.[Category], p.[Address], p.[Notes], p.[PlannedOn], p.[CreatedAt],
                           CASE
                               WHEN p.[DestinationId] IS NULL THEN NULL
                               WHEN d.[ParentDestinationId] IS NULL THEN d.[Id]
                               ELSE d.[ParentDestinationId]
                           END AS [ParentId],
                           CASE WHEN p.[DestinationId] IS NULL THEN 'Place' ELSE 'Stop' END AS [Kind]
                    FROM [SavedPlaces] p
                    LEFT JOIN [TripDestinations] d ON d.[Id] = p.[DestinationId]
                ),
                [Ordered] AS (
                    SELECT s.*,
                           ROW_NUMBER() OVER (
                               PARTITION BY s.[TripId], s.[Kind], s.[ParentId]
                               ORDER BY s.[CreatedAt], s.[Id]) - 1 AS [Sequence]
                    FROM [Source] s
                ),
                [ExistingMax] AS (
                    SELECT d.[TripId], d.[Kind], d.[ParentDestinationId],
                           MAX(d.[SortOrder]) AS [MaxOrder]
                    FROM [TripDestinations] d
                    GROUP BY d.[TripId], d.[Kind], d.[ParentDestinationId]
                )
                INSERT INTO [TripDestinations]
                    ([Id], [TripId], [ParentDestinationId], [Kind], [Name], [SortOrder],
                     [StartsOn], [EndsOn], [Latitude], [Longitude], [Category], [Address],
                     [Notes], [PlannedOn], [CreatedAt], [MigratedFromSavedPlace])
                SELECT o.[Id], o.[TripId], o.[ParentId], o.[Kind], o.[Name],
                       ISNULL(m.[MaxOrder], -1) + 1 + o.[Sequence],
                       NULL, NULL, o.[Latitude], o.[Longitude], o.[Category], o.[Address],
                       o.[Notes], o.[PlannedOn], o.[CreatedAt], 1
                FROM [Ordered] o
                LEFT JOIN [ExistingMax] m
                    ON m.[TripId] = o.[TripId]
                   AND m.[Kind] = o.[Kind]
                   AND ((m.[ParentDestinationId] = o.[ParentId])
                        OR (m.[ParentDestinationId] IS NULL AND o.[ParentId] IS NULL));
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_TripId_Kind_ParentDestinationId_SortOrder",
                table: "TripDestinations",
                columns: KindIndexColumns,
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDestinations_Kind",
                table: "TripDestinations",
                sql: "([Kind] = 'Stop' AND [ParentDestinationId] IS NOT NULL) OR ([Kind] IN ('Destination', 'Place') AND [ParentDestinationId] IS NULL)");

            // Keep the old table as a read-only archive for one release, but
            // take away the foreign keys that would otherwise block a
            // destination deletion now that the app no longer maps it.
            migrationBuilder.RenameTable(
                name: "SavedPlaces",
                newName: "SavedPlaces_Legacy");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedPlaces_TripDestinations_DestinationId",
                table: "SavedPlaces_Legacy");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedPlaces_Trips_TripId",
                table: "SavedPlaces_Legacy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripDestinations_TripId_Kind_ParentDestinationId_SortOrder",
                table: "TripDestinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDestinations_Kind",
                table: "TripDestinations");

            // The archive keeps the original constraint and index names, so it
            // is dropped before the original table is recreated. Its data has
            // already been copied into the unified table, and the copy back
            // below reads from there.
            migrationBuilder.DropTable(
                name: "SavedPlaces_Legacy");

            migrationBuilder.CreateTable(
                name: "SavedPlaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DestinationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PlannedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedPlaces", x => x.Id);
                    table.CheckConstraint("CK_SavedPlaces_Coordinates", "[Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "FK_SavedPlaces_TripDestinations_DestinationId",
                        column: x => x.DestinationId,
                        principalTable: "TripDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedPlaces_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Move only the rows this migration copied back to the old table.
            // A stop that was already part of the hierarchy is not touched.
            migrationBuilder.Sql(
                """
                INSERT INTO [SavedPlaces]
                    ([Id], [TripId], [DestinationId], [Name], [Latitude], [Longitude],
                     [Category], [Address], [Notes], [PlannedOn], [CreatedAt])
                SELECT [Id], [TripId],
                       CASE WHEN [Kind] = 'Stop' THEN [ParentDestinationId] ELSE NULL END,
                       [Name], [Latitude], [Longitude], [Category], [Address], [Notes],
                       [PlannedOn], ISNULL([CreatedAt], SYSDATETIMEOFFSET())
                FROM [TripDestinations]
                WHERE [MigratedFromSavedPlace] = 1;
                """);

            migrationBuilder.Sql(
                "DELETE FROM [TripDestinations] WHERE [MigratedFromSavedPlace] = 1;");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "MigratedFromSavedPlace",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "PlannedOn",
                table: "TripDestinations");

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_TripId_ParentDestinationId_SortOrder",
                table: "TripDestinations",
                columns: RouteIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavedPlaces_DestinationId",
                table: "SavedPlaces",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedPlaces_TripId",
                table: "SavedPlaces",
                column: "TripId");
        }
    }
}
