using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260706103000_UpdateLeadTaskDelegationFinalReviewConstraint")]
    public partial class UpdateLeadTaskDelegationFinalReviewConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  DROP CONSTRAINT IF EXISTS ""CK_LeadTaskDelegationRequests_ExactlyOneTask"";");

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  ADD CONSTRAINT ""CK_LeadTaskDelegationRequests_ExactlyOneTask""
                  CHECK (
                      (""Type"" = 0 AND ""MaterialDeliveryId"" IS NOT NULL AND ""TransferRequestId"" IS NULL)
                      OR (""Type"" = 1 AND ""TransferRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL)
                      OR (""Type"" = 2 AND ""MaterialDeliveryId"" IS NULL AND ""TransferRequestId"" IS NULL)
                  );");

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_LeadTaskDelegationRequests_Type_BatchId_Status""
                  ON ""LeadTaskDelegationRequests"" (""Type"", ""BatchId"", ""Status"");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP INDEX IF EXISTS ""IX_LeadTaskDelegationRequests_Type_BatchId_Status"";");

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  DROP CONSTRAINT IF EXISTS ""CK_LeadTaskDelegationRequests_ExactlyOneTask"";");

            migrationBuilder.Sql(
                @"ALTER TABLE ""LeadTaskDelegationRequests""
                  ADD CONSTRAINT ""CK_LeadTaskDelegationRequests_ExactlyOneTask""
                  CHECK (
                      (""Type"" = 0 AND ""MaterialDeliveryId"" IS NOT NULL AND ""TransferRequestId"" IS NULL)
                      OR (""Type"" = 1 AND ""TransferRequestId"" IS NOT NULL AND ""MaterialDeliveryId"" IS NULL)
                  );");
        }
    }
}
