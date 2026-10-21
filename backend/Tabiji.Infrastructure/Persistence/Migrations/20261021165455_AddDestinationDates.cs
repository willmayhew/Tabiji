using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDestinationDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EndsOn",
                table: "TripDestinations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartsOn",
                table: "TripDestinations",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDestinations_Dates",
                table: "TripDestinations",
                sql: "[StartsOn] IS NULL OR [EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDestinations_Dates",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "EndsOn",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "StartsOn",
                table: "TripDestinations");
        }
    }
}
