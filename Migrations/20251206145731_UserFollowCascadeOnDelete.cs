using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroSocialPlatform.Migrations
{
    /// <inheritdoc />
    public partial class UserFollowCascadeOnDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFollows_AspNetUsers_ObserverId",
                table: "UserFollows");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFollows_AspNetUsers_TargetId",
                table: "UserFollows");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFollows_AspNetUsers_ObserverId",
                table: "UserFollows",
                column: "ObserverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFollows_AspNetUsers_TargetId",
                table: "UserFollows",
                column: "TargetId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFollows_AspNetUsers_ObserverId",
                table: "UserFollows");

            migrationBuilder.DropForeignKey(
                name: "FK_UserFollows_AspNetUsers_TargetId",
                table: "UserFollows");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFollows_AspNetUsers_ObserverId",
                table: "UserFollows",
                column: "ObserverId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserFollows_AspNetUsers_TargetId",
                table: "UserFollows",
                column: "TargetId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
