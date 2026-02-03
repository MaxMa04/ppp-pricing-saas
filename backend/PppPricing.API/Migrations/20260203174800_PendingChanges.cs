using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PppPricing.API.Migrations
{
    /// <inheritdoc />
    public partial class PendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_RegionCode_UserId",
                table: "PppMultipliers");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataDate",
                table: "PppMultipliers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "IndexType",
                table: "PppMultipliers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PreferredIndexType",
                table: "Apps",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PricingIndexRawData",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IndexType = table.Column<int>(type: "INTEGER", nullable: false),
                    RegionCode = table.Column<string>(type: "TEXT", nullable: false),
                    CountryName = table.Column<string>(type: "TEXT", nullable: true),
                    CurrencyCode = table.Column<string>(type: "TEXT", nullable: true),
                    LocalPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    UsdPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "TEXT", nullable: true),
                    HourlyWage = table.Column<decimal>(type: "TEXT", nullable: true),
                    WorkingHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    PlanType = table.Column<string>(type: "TEXT", nullable: true),
                    DataDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingIndexRawData", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_RegionCode_UserId_IndexType",
                table: "PppMultipliers",
                columns: new[] { "RegionCode", "UserId", "IndexType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingIndexRawData_IndexType_RegionCode_PlanType",
                table: "PricingIndexRawData",
                columns: new[] { "IndexType", "RegionCode", "PlanType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingIndexRawData");

            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_RegionCode_UserId_IndexType",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "DataDate",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "IndexType",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "PreferredIndexType",
                table: "Apps");

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_RegionCode_UserId",
                table: "PppMultipliers",
                columns: new[] { "RegionCode", "UserId" },
                unique: true);
        }
    }
}
