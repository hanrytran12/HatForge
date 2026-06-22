using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                table: "TransferRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmedByQCId",
                table: "TransferRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByQCId",
                table: "TransferRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_ConfirmedByQCId",
                table: "TransferRequests",
                column: "ConfirmedByQCId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferRequests_CreatedByQCId",
                table: "TransferRequests",
                column: "CreatedByQCId");

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Users_ConfirmedByQCId",
                table: "TransferRequests",
                column: "ConfirmedByQCId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferRequests_Users_CreatedByQCId",
                table: "TransferRequests",
                column: "CreatedByQCId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Users_ConfirmedByQCId",
                table: "TransferRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferRequests_Users_CreatedByQCId",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_ConfirmedByQCId",
                table: "TransferRequests");

            migrationBuilder.DropIndex(
                name: "IX_TransferRequests_CreatedByQCId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "ConfirmedByQCId",
                table: "TransferRequests");

            migrationBuilder.DropColumn(
                name: "CreatedByQCId",
                table: "TransferRequests");
        }
    }
}
