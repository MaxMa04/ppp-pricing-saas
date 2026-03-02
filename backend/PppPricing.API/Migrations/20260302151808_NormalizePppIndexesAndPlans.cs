using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PppPricing.API.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePppIndexesAndPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_RegionCode_UserId_IndexType",
                table: "PppMultipliers");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "PppMultipliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanType",
                table: "PppMultipliers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredNetflixPlan",
                table: "Apps",
                type: "TEXT",
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.Sql("UPDATE Apps SET PreferredNetflixPlan = 'standard' WHERE PreferredNetflixPlan IS NULL OR PreferredNetflixPlan = '';");
            migrationBuilder.Sql("UPDATE PppMultipliers SET PlanType = 'standard' WHERE IndexType = 1 AND (PlanType IS NULL OR PlanType = '');");

            var alpha2ToAlpha3 = new Dictionary<string, string>
            {
                { "AR", "ARG" }, { "AU", "AUS" }, { "AT", "AUT" }, { "AZ", "AZE" },
                { "BH", "BHR" }, { "BR", "BRA" }, { "GB", "GBR" }, { "CA", "CAN" },
                { "CL", "CHL" }, { "CN", "CHN" }, { "CO", "COL" }, { "CR", "CRI" },
                { "HR", "HRV" }, { "CZ", "CZE" }, { "DK", "DNK" }, { "EG", "EGY" },
                { "EU", "EUZ" }, { "GT", "GTM" }, { "HN", "HND" }, { "HK", "HKG" },
                { "HU", "HUN" }, { "IN", "IND" }, { "ID", "IDN" }, { "IL", "ISR" },
                { "JP", "JPN" }, { "JO", "JOR" }, { "KW", "KWT" }, { "LB", "LBN" },
                { "MY", "MYS" }, { "MX", "MEX" }, { "MD", "MDA" }, { "NI", "NIC" },
                { "NO", "NOR" }, { "OM", "OMN" }, { "PK", "PAK" }, { "PE", "PER" },
                { "PH", "PHL" }, { "PL", "POL" }, { "QA", "QAT" }, { "RO", "ROU" },
                { "RU", "RUS" }, { "SA", "SAU" }, { "SG", "SGP" }, { "ZA", "ZAF" },
                { "KR", "KOR" }, { "LK", "LKA" }, { "SE", "SWE" }, { "CH", "CHE" },
                { "TW", "TWN" }, { "TH", "THA" }, { "TR", "TUR" }, { "AE", "ARE" },
                { "UA", "UKR" }, { "UY", "URY" }, { "US", "USA" }, { "VE", "VEN" },
                { "VN", "VNM" }, { "NZ", "NZL" }
            };

            foreach (var (alpha2, alpha3) in alpha2ToAlpha3)
            {
                migrationBuilder.Sql($@"UPDATE PppMultipliers SET RegionCode = '{alpha3}' WHERE RegionCode = '{alpha2}';");
                migrationBuilder.Sql($@"UPDATE PricingIndexRawData SET RegionCode = '{alpha3}' WHERE RegionCode = '{alpha2}';");
            }

            migrationBuilder.Sql(@"
                UPDATE PppMultipliers
                SET CurrencyCode = (
                    SELECT r.CurrencyCode
                    FROM PricingIndexRawData r
                    WHERE r.IndexType = PppMultipliers.IndexType
                      AND r.RegionCode = PppMultipliers.RegionCode
                      AND (
                        (r.PlanType = PppMultipliers.PlanType)
                        OR (r.PlanType IS NULL AND PppMultipliers.PlanType IS NULL)
                      )
                    LIMIT 1
                )
                WHERE CurrencyCode IS NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE PppMultipliers
                SET Multiplier = (
                    SELECT CASE
                        WHEN us.UsdPrice > 0 THEN r.UsdPrice / us.UsdPrice
                        ELSE PppMultipliers.Multiplier
                    END
                    FROM PricingIndexRawData r
                    JOIN PricingIndexRawData us
                        ON us.IndexType = 0
                        AND us.RegionCode = 'USA'
                        AND us.PlanType IS NULL
                    WHERE r.IndexType = 0
                      AND r.RegionCode = PppMultipliers.RegionCode
                      AND r.PlanType IS NULL
                    LIMIT 1
                )
                WHERE PppMultipliers.IndexType = 0
                  AND PppMultipliers.UserId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_RegionCode_UserId_IndexType_PlanType",
                table: "PppMultipliers",
                columns: new[] { "RegionCode", "UserId", "IndexType", "PlanType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PppMultipliers_RegionCode_UserId_IndexType_PlanType",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "PlanType",
                table: "PppMultipliers");

            migrationBuilder.DropColumn(
                name: "PreferredNetflixPlan",
                table: "Apps");

            migrationBuilder.Sql("UPDATE PppMultipliers SET PlanType = NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_PppMultipliers_RegionCode_UserId_IndexType",
                table: "PppMultipliers",
                columns: new[] { "RegionCode", "UserId", "IndexType" },
                unique: true);
        }
    }
}
