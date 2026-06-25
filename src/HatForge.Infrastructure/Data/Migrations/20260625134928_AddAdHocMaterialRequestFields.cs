using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdHocMaterialRequestFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OriginalDeliveryId",
                table: "MaterialRequests",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<bool>(
                name: "IsAdHoc",
                table: "MaterialRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "MaterialRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkshopId",
                table: "MaterialRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_WorkshopId",
                table: "MaterialRequests",
                column: "WorkshopId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_Workshops_WorkshopId",
                table: "MaterialRequests",
                column: "WorkshopId",
                principalTable: "Workshops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_Workshops_WorkshopId",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_WorkshopId",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "IsAdHoc",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "WorkshopId",
                table: "MaterialRequests");

            migrationBuilder.AlterColumn<int>(
                name: "OriginalDeliveryId",
                table: "MaterialRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
