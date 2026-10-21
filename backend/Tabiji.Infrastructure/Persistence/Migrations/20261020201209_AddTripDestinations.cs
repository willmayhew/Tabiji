using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripDestinations : Migration
    {
        /// <summary>
        /// The columns of the unique index that keeps a trip from holding two
        /// stops in the same position.
        /// </summary>
        private static readonly string[] RouteIndexColumns = ["TripId", "SortOrder"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The legacy scalar column held the only copy of a trip's
            // destination. Nothing writes it any more, so it is made nullable
            // rather than dropped: no data is lost, and the column can be
            // removed in its own migration once the backfill below has been
            // verified.
            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "Trips",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateTable(
                name: "TripDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripDestinations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_TripId_SortOrder",
                table: "TripDestinations",
                columns: RouteIndexColumns,
                unique: true);

            // Every existing destination becomes the first stop of its trip.
            //
            // A blank or whitespace-only destination produces no stop: making
            // one up would invent data. Such a trip simply has no stops, which
            // the interface renders as an explicit empty state.
            migrationBuilder.Sql(
                """
                INSERT INTO [TripDestinations] ([Id], [TripId], [Name], [SortOrder])
                SELECT NEWID(), [Id], LTRIM(RTRIM([Destination])), 0
                FROM [Trips]
                WHERE [Destination] IS NOT NULL
                  AND LTRIM(RTRIM([Destination])) <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Put the legacy scalar back before the route disappears. The
            // first stop of each trip becomes its destination again, and a
            // trip that never had one falls back to a placeholder so that the
            // column can be made non-nullable again.
            migrationBuilder.Sql(
                """
                UPDATE [t]
                SET [t].[Destination] = ISNULL(
                    (SELECT TOP 1 [d].[Name]
                     FROM [TripDestinations] [d]
                     WHERE [d].[TripId] = [t].[Id]
                     ORDER BY [d].[SortOrder]),
                    'Unknown')
                FROM [Trips] [t];
                """);

            migrationBuilder.DropTable(
                name: "TripDestinations");

            // No default value: the statement above has already given every
            // row a destination, so the column can be made non-nullable
            // outright.
            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "Trips",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
