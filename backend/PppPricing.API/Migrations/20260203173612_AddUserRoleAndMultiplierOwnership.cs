using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PppPricing.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleAndMultiplierOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_RegionCode",
                table: "PppMultipliers");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "PppMultipliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_RegionCode_UserId",
                table: "PppMultipliers",
                columns: new[] { "RegionCode", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_UserId",
                table: "PppMultipliers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PppMultipliers_Users_UserId",
                table: "PppMultipliers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PppMultipliers_Users_UserId",
                table: "PppMultipliers");

            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_RegionCode_UserId",
                table: "PppMultipliers");

            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_UserId",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PppMultipliers");

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_RegionCode",
                table: "PppMultipliers",
                column: "RegionCode",
                unique: true);
        }
    }
}
