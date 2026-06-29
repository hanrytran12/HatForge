using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260629133000_AddTransferReceiptInspectionQuantities")]
    public partial class AddTransferReceiptInspectionQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceivedDefectiveQuantity",
                table: "TransferRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptInspectionNotes",
                table: "TransferRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReceivedUsableQuantity",
                table: "TransferRequests",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceivedDefectiveQuantity",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ReceiptInspectionNotes",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ReceivedUsableQuantity",
                table: "TransferRequests");
        }
    }
}
