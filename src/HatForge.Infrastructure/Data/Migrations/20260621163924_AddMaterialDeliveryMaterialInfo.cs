using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialDeliveryMaterialInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaterialName",
                table: "MaterialDeliveries",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PlannedQuantity",
                table: "MaterialDeliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "MaterialDeliveries");

            migrationBuilder.DropColumn(
                name: "PlannedQuantity",
                table: "MaterialDeliveries");
        }
    }
}
