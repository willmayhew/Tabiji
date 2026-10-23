using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDestinationCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "TripDestinations",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "TripDestinations",
                type: "float",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDestinations_Coordinates",
                table: "TripDestinations",
                sql: "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDestinations_Coordinates",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "TripDestinations");
        }
    }
}
