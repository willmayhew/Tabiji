using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Gives every trip a membership row, so that a trip is private to the
    /// accounts that may read it.
    ///
    /// The change is additive: the table is new, and nothing that already
    /// exists is altered or dropped. The legacy <c>Trips.Destination</c>
    /// column and the <c>SavedPlaces_Legacy</c> archive are untouched.
    ///
    /// Trips created before accounts existed are assigned to the account that
    /// is already there, and only when that account is unambiguous. Otherwise
    /// they are left unowned rather than guessed at: <c>verify-migration.sql</c>
    /// reports every trip without an owner.
    /// </summary>
    public partial class AddTripMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TripMembers",
                columns: table => new
                {
                    TripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripMembers", x => new { x.TripId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TripMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripMembers_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripMembers_UserId",
                table: "TripMembers",
                column: "UserId");

            // Trips that existed before accounts did must not be stranded.
            // There is exactly one account in a database that predates
            // ownership, and it is the only person who could have created
            // them, so they are assigned to it.
            //
            // No account, or more than one, is a human decision: the trips are
            // left unowned rather than guessed at, the migration still
            // applies, and verify-migration.sql reports every unowned trip.
            // Refusing here would block an upgrade over a database whose only
            // sin is that nobody has signed up yet.
            migrationBuilder.Sql(
                """
                IF (SELECT COUNT(*) FROM [AspNetUsers]) = 1
                BEGIN
                    INSERT INTO [TripMembers] ([TripId], [UserId], [Role], [GrantedAt])
                    SELECT t.[Id], (SELECT [Id] FROM [AspNetUsers]), 'Owner', SYSDATETIMEOFFSET()
                    FROM [Trips] t
                    WHERE NOT EXISTS (SELECT 1 FROM [TripMembers] m WHERE m.[TripId] = t.[Id]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The trips themselves are untouched by the rollback; only the
            // record of who owned them goes.
            migrationBuilder.DropTable(
                name: "TripMembers");
        }
    }
}
