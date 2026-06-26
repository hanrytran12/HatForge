using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchWorkshopMaterialTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualMaterialUsed",
                table: "Works",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InitialMaterialQty",
                table: "BatchWorkshops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaterialUsed",
                table: "BatchWorkshops",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualMaterialUsed",
                table: "Works");

            migrationBuilder.DropColumn(
                name: "InitialMaterialQty",
                table: "BatchWorkshops");

            migrationBuilder.DropColumn(
                name: "MaterialUsed",
                table: "BatchWorkshops");
        }
    }
}
