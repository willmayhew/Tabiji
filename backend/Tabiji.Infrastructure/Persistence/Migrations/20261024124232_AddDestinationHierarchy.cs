using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tabiji.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDestinationHierarchy : Migration
    {
        private static readonly string[] ScopeIndexColumns = ["TripId", "ParentDestinationId", "SortOrder"];

        private static readonly string[] RouteIndexColumns = ["TripId", "SortOrder"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripDestinations_TripId_SortOrder",
                table: "TripDestinations");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentDestinationId",
                table: "TripDestinations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_ParentDestinationId",
                table: "TripDestinations",
                column: "ParentDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_TripId_ParentDestinationId_SortOrder",
                table: "TripDestinations",
                columns: ScopeIndexColumns,
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripDestinations_Parent",
                table: "TripDestinations",
                sql: "[ParentDestinationId] IS NULL OR [ParentDestinationId] <> [Id]");

            migrationBuilder.AddForeignKey(
                name: "FK_TripDestinations_TripDestinations_ParentDestinationId",
                table: "TripDestinations",
                column: "ParentDestinationId",
                principalTable: "TripDestinations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A rollback cannot silently destroy the stops a reader has added.
            // Flatten or remove them first, deliberately, and then roll back.
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM [TripDestinations] WHERE [ParentDestinationId] IS NOT NULL)
                    THROW 50000, 'Cannot roll back AddDestinationHierarchy while nested stops exist. Flatten or remove them first.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TripDestinations_TripDestinations_ParentDestinationId",
                table: "TripDestinations");

            migrationBuilder.DropIndex(
                name: "IX_TripDestinations_ParentDestinationId",
                table: "TripDestinations");

            migrationBuilder.DropIndex(
                name: "IX_TripDestinations_TripId_ParentDestinationId_SortOrder",
                table: "TripDestinations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripDestinations_Parent",
                table: "TripDestinations");

            migrationBuilder.DropColumn(
                name: "ParentDestinationId",
                table: "TripDestinations");

            migrationBuilder.CreateIndex(
                name: "IX_TripDestinations_TripId_SortOrder",
                table: "TripDestinations",
                columns: RouteIndexColumns,
                unique: true);
        }
    }
}
