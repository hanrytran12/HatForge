using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_Users_CreatedByQCId",
                table: "MaterialRequests");

            migrationBuilder.AddColumn<int>(
                name: "MaterialDeliveryId",
                table: "MaterialRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_MaterialDeliveryId",
                table: "MaterialRequests",
                column: "MaterialDeliveryId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_MaterialDeliveries_MaterialDeliveryId",
                table: "MaterialRequests",
                column: "MaterialDeliveryId",
                principalTable: "MaterialDeliveries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_Users_CreatedByQCId",
                table: "MaterialRequests",
                column: "CreatedByQCId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_MaterialDeliveries_MaterialDeliveryId",
                table: "MaterialRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_Users_CreatedByQCId",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_MaterialDeliveryId",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "MaterialDeliveryId",
                table: "MaterialRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_Users_CreatedByQCId",
                table: "MaterialRequests",
                column: "CreatedByQCId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
