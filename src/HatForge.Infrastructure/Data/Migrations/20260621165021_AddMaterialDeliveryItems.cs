using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialDeliveryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveredQuantity",
                table: "MaterialDeliveries");

            migrationBuilder.DropColumn(
                name: "MaterialName",
                table: "MaterialDeliveries");

            migrationBuilder.DropColumn(
                name: "PlannedQuantity",
                table: "MaterialDeliveries");

            migrationBuilder.CreateTable(
                name: "MaterialDeliveryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MaterialDeliveryId = table.Column<int>(type: "integer", nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PlannedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ActualQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialDeliveryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialDeliveryItems_MaterialDeliveries_MaterialDeliveryId",
                        column: x => x.MaterialDeliveryId,
                        principalTable: "MaterialDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialDeliveryItems_MaterialDeliveryId",
                table: "MaterialDeliveryItems",
                column: "MaterialDeliveryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialDeliveryItems");

            migrationBuilder.AddColumn<int>(
                name: "DeliveredQuantity",
                table: "MaterialDeliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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
    }
}
