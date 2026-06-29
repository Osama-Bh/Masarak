using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GoWork.Migrations
{
    /// <inheritdoc />
    public partial class updateCandidateDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TbApplications_TbSeekers_SeekerId",
                table: "TbApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_TbDeviceTokens_AspNetUsers_UserId",
                table: "TbDeviceTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_TbFeedbacks_AspNetUsers_ReviewerId",
                table: "TbFeedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_TbInterviews_TbApplications_ApplicationId",
                table: "TbInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TbSeekers_TbAddresses_AddressId",
                table: "TbSeekers");

            migrationBuilder.DropForeignKey(
                name: "FK_TbSeekerSkills_TbSeekers_SeekerId",
                table: "TbSeekerSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_TbUserNotifications_AspNetUsers_UserId",
                table: "TbUserNotifications");

            migrationBuilder.AddForeignKey(
                name: "FK_TbApplications_TbSeekers_SeekerId",
                table: "TbApplications",
                column: "SeekerId",
                principalTable: "TbSeekers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TbDeviceTokens_AspNetUsers_UserId",
                table: "TbDeviceTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TbFeedbacks_AspNetUsers_ReviewerId",
                table: "TbFeedbacks",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TbInterviews_TbApplications_ApplicationId",
                table: "TbInterviews",
                column: "ApplicationId",
                principalTable: "TbApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TbSeekers_TbAddresses_AddressId",
                table: "TbSeekers",
                column: "AddressId",
                principalTable: "TbAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TbSeekerSkills_TbSeekers_SeekerId",
                table: "TbSeekerSkills",
                column: "SeekerId",
                principalTable: "TbSeekers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TbUserNotifications_AspNetUsers_UserId",
                table: "TbUserNotifications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TbApplications_TbSeekers_SeekerId",
                table: "TbApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_TbDeviceTokens_AspNetUsers_UserId",
                table: "TbDeviceTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_TbFeedbacks_AspNetUsers_ReviewerId",
                table: "TbFeedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_TbInterviews_TbApplications_ApplicationId",
                table: "TbInterviews");

            migrationBuilder.DropForeignKey(
                name: "FK_TbSeekers_TbAddresses_AddressId",
                table: "TbSeekers");

            migrationBuilder.DropForeignKey(
                name: "FK_TbSeekerSkills_TbSeekers_SeekerId",
                table: "TbSeekerSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_TbUserNotifications_AspNetUsers_UserId",
                table: "TbUserNotifications");

            migrationBuilder.AddForeignKey(
                name: "FK_TbApplications_TbSeekers_SeekerId",
                table: "TbApplications",
                column: "SeekerId",
                principalTable: "TbSeekers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TbDeviceTokens_AspNetUsers_UserId",
                table: "TbDeviceTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TbFeedbacks_AspNetUsers_ReviewerId",
                table: "TbFeedbacks",
                column: "ReviewerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TbInterviews_TbApplications_ApplicationId",
                table: "TbInterviews",
                column: "ApplicationId",
                principalTable: "TbApplications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TbSeekers_TbAddresses_AddressId",
                table: "TbSeekers",
                column: "AddressId",
                principalTable: "TbAddresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TbSeekerSkills_TbSeekers_SeekerId",
                table: "TbSeekerSkills",
                column: "SeekerId",
                principalTable: "TbSeekers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TbUserNotifications_AspNetUsers_UserId",
                table: "TbUserNotifications",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
