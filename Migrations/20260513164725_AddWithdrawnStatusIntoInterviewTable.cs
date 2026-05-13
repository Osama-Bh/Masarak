using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class AddWithdrawnStatusIntoInterviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "TbInterviewStatuses",
                columns: new[] { "Id", "IsActive", "Name", "SortOrder" },
                values: new object[] { 8, true, "Withdrawn", 80 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TbInterviewStatuses",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
