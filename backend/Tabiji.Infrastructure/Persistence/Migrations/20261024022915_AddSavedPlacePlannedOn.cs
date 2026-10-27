using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedPlacePlannedOn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedOn",
                table: "SavedPlaces",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlannedOn",
                table: "SavedPlaces");
        }
    }
}
