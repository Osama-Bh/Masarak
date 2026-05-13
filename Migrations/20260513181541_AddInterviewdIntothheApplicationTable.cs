using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewdIntothheApplicationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "TbApplicationStatuses",
                columns: new[] { "Id", "IsActive", "Name", "SortOrder" },
                values: new object[] { 10, true, "Interviewed", 100 });

            //migrationBuilder.InsertData(
            //    table: "TbInterviewStatuses",
            //    columns: new[] { "Id", "IsActive", "Name", "SortOrder" },
            //    values: new object[] { 8, true, "Withdrawn", 80 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TbApplicationStatuses",
                keyColumn: "Id",
                keyValue: 10);

            //migrationBuilder.DeleteData(
            //    table: "TbInterviewStatuses",
            //    keyColumn: "Id",
            //    keyValue: 8);
        }
    }
}
