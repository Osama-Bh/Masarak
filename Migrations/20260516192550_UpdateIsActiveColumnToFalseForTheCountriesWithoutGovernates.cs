using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class UpdateIsActiveColumnToFalseForTheCountriesWithoutGovernates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE c
                SET c.IsActive = 0
                FROM TbCountries c
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM TbGovernates g
                    WHERE g.CountryId = c.Id
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE c
                SET c.IsActive = 1
                FROM TbCountries c
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM TbGovernates g
                    WHERE g.CountryId = c.Id
                );
            ");
        }
    }
}
