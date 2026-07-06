using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadTaskDelegations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadTaskDelegationRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BatchId = table.Column<int>(type: "integer", nullable: false),
                    MaterialDeliveryId = table.Column<int>(type: "integer", nullable: true),
                    TransferRequestId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedByLeadId = table.Column<int>(type: "integer", nullable: false),
                    AssignedTransportQcId = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByAdminId = table.Column<int>(type: "integer", nullable: true),
                    CompletedByTransportQcId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadTaskDelegationRequests", x => x.Id);
                    table.CheckConstraint("CK_LeadTaskDelegationRequests_ExactlyOneTask", "(\"Type\" = 0 AND \"MaterialDeliveryId\" IS NOT NULL AND \"TransferRequestId\" IS NULL)\n                      OR (\"Type\" = 1 AND \"TransferRequestId\" IS NOT NULL AND \"MaterialDeliveryId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_MaterialDeliveries_MaterialDeliv~",
                        column: x => x.MaterialDeliveryId,
                        principalTable: "MaterialDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_TransferRequests_TransferRequest~",
                        column: x => x.TransferRequestId,
                        principalTable: "TransferRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_Users_AssignedTransportQcId",
                        column: x => x.AssignedTransportQcId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_Users_CompletedByTransportQcId",
                        column: x => x.CompletedByTransportQcId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_Users_RequestedByLeadId",
                        column: x => x.RequestedByLeadId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadTaskDelegationRequests_Users_ReviewedByAdminId",
                        column: x => x.ReviewedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_AssignedTransportQcId",
                table: "LeadTaskDelegationRequests",
                column: "AssignedTransportQcId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_BatchId",
                table: "LeadTaskDelegationRequests",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_CompletedByTransportQcId",
                table: "LeadTaskDelegationRequests",
                column: "CompletedByTransportQcId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_MaterialDeliveryId",
                table: "LeadTaskDelegationRequests",
                column: "MaterialDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_RequestedByLeadId",
                table: "LeadTaskDelegationRequests",
                column: "RequestedByLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_ReviewedByAdminId",
                table: "LeadTaskDelegationRequests",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_TransferRequestId",
                table: "LeadTaskDelegationRequests",
                column: "TransferRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_Type_MaterialDeliveryId_Status",
                table: "LeadTaskDelegationRequests",
                columns: new[] { "Type", "MaterialDeliveryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadTaskDelegationRequests_Type_TransferRequestId_Status",
                table: "LeadTaskDelegationRequests",
                columns: new[] { "Type", "TransferRequestId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadTaskDelegationRequests");
        }
    }
}
