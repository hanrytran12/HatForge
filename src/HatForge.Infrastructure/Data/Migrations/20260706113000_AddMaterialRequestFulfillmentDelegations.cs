using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706113000_AddMaterialRequestFulfillmentDelegations")]
    public partial class AddMaterialRequestFulfillmentDelegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaterialRequestId",
                table: "LeadTaskDelegationRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  DROP CONSTRAINT IF EXISTS ""CK_LeadTaskDelegationRequests_ExactlyOneTask"";");

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  ADD CONSTRAINT ""CK_LeadTaskDelegationRequests_ExactlyOneTask""
                  CHECK (
                      (""Type"" = 0 AND ""MaterialDeliveryId"" IS NOT NULL AND ""TransferRequestId"" IS NULL AND ""MaterialRequestId"" IS NULL)
                      OR (""Type"" = 1 AND ""TransferRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL AND ""MaterialRequestId"" IS NULL)
                      OR (""Type"" = 2 AND ""MaterialDeliveryId"" IS NULL AND ""TransferRequestId"" IS NULL AND ""MaterialRequestId"" IS NULL)
                      OR (""Type"" = 3 AND ""MaterialRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL AND ""TransferRequestId"" IS NULL)
                  );");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialRequestId",
                table: "LeadTaskDelegationRequests",
                column: "MaterialRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_Type_MaterialRequestId_Status",
                table: "LeadTaskDelegationRequests",
                columns: new[] { "Type", "MaterialRequestId", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_LeadTaskDelegationRequests_MaterialRequests_MaterialRequestId",
                table: "LeadTaskDelegationRequests",
                column: "MaterialRequestId",
                principalTable: "MaterialRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeadTaskDelegationRequests_MaterialRequests_MaterialRequestId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialRequestId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeadTaskDelegationRequests_Type_MaterialRequestId_Status",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  DROP CONSTRAINT IF EXISTS ""CK_LeadTaskDelegationRequests_ExactlyOneTask"";");

            migrationBuilder.DropColumn(
                name: "MaterialRequestId",
                table: "LeadTaskDelegationRequests");

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  ADD CONSTRAINT ""CK_LeadTaskDelegationRequests_ExactlyOneTask""
                  CHECK (
                      (""Type"" = 0 AND ""MaterialDeliveryId"" IS NOT NULL AND ""TransferRequestId"" IS NULL)
                      OR (""Type"" = 1 AND ""TransferRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL)
                      OR (""Type"" = 2 AND ""MaterialDeliveryId"" IS NULL AND ""TransferRequestId"" IS NULL)
                  );");
        }
    }
}
