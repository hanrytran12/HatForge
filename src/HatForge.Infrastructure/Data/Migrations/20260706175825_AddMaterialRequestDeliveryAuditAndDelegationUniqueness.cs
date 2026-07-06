using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialRequestDeliveryAuditAndDelegationUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LeadTaskDelegationRequests_BatchId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialDeliveryId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialRequestId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeadTaskDelegationRequests_TransferRequestId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAt",
                table: "MaterialRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveredByTransportQcId",
                table: "MaterialRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE ""MaterialRequests"" mr
                  SET ""DeliveredByTransportQcId"" = d.""CompletedByTransportQcId"",
                      ""DeliveredAt"" = d.""CompletedAt""
                  FROM ""LeadTaskDelegationRequests"" d
                  WHERE d.""Type"" = 3
                    AND d.""Status"" = 3
                    AND d.""MaterialRequestId"" = mr.""Id""
                    AND mr.""DeliveredAt"" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialRequests_DeliveredByTransportQcId",
                table: "MaterialRequests",
                column: "DeliveredByTransportQcId");

            migrationBuilder.CreateIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveFinalReview",
                table: "LeadTaskDelegationRequests",
                column: "BatchId",
                unique: true,
                filter: "\"Type\" = 2 AND \"Status\" IN (0, 1)");

            migrationBuilder.CreateIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveMaterialDelivery",
                table: "LeadTaskDelegationRequests",
                column: "MaterialDeliveryId",
                unique: true,
                filter: "\"Type\" = 0 AND \"Status\" IN (0, 1) AND \"MaterialDeliveryId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveMaterialRequestFulfillment",
                table: "LeadTaskDelegationRequests",
                column: "MaterialRequestId",
                unique: true,
                filter: "\"Type\" = 3 AND \"Status\" IN (0, 1, 3) AND \"MaterialRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveTransferApproval",
                table: "LeadTaskDelegationRequests",
                column: "TransferRequestId",
                unique: true,
                filter: "\"Type\" = 1 AND \"Status\" IN (0, 1) AND \"TransferRequestId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialRequests_Users_DeliveredByTransportQcId",
                table: "MaterialRequests",
                column: "DeliveredByTransportQcId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialRequests_Users_DeliveredByTransportQcId",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialRequests_DeliveredByTransportQcId",
                table: "MaterialRequests");

            migrationBuilder.DropIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveFinalReview",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveMaterialDelivery",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveMaterialRequestFulfillment",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "UX_LeadTaskDelegationRequests_ActiveTransferApproval",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "MaterialRequests");

            migrationBuilder.DropColumn(
                name: "DeliveredByTransportQcId",
                table: "MaterialRequests");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_BatchId",
                table: "LeadTaskDelegationRequests",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialDeliveryId",
                table: "LeadTaskDelegationRequests",
                column: "MaterialDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialRequestId",
                table: "LeadTaskDelegationRequests",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_TransferRequestId",
                table: "LeadTaskDelegationRequests",
                column: "TransferRequestId");
        }
    }
}
