using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadMaterialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "MaterialDeliveryItems",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "m");

            migrationBuilder.CreateTable(
                name: "LeadMaterialStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedMaterialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadMaterialStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStocks_Users_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeadMaterialStockTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LeadMaterialStockId = table.Column<int>(type: "integer", nullable: false),
                    LeadId = table.Column<int>(type: "integer", nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedMaterialName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<int>(type: "integer", nullable: true),
                    MaterialDeliveryId = table.Column<int>(type: "integer", nullable: true),
                    MaterialRequestId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadMaterialStockTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStockTransactions_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStockTransactions_LeadMaterialStocks_LeadMateri~",
                        column: x => x.LeadMaterialStockId,
                        principalTable: "LeadMaterialStocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStockTransactions_MaterialDeliveries_MaterialDe~",
                        column: x => x.MaterialDeliveryId,
                        principalTable: "MaterialDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStockTransactions_MaterialRequests_MaterialRequ~",
                        column: x => x.MaterialRequestId,
                        principalTable: "MaterialRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStockTransactions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeadMaterialStockTransactions_Users_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStocks_LeadId_NormalizedMaterialName_Unit",
                table: "LeadMaterialStocks",
                columns: new[] { "LeadId", "NormalizedMaterialName", "Unit" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStockTransactions_BatchId",
                table: "LeadMaterialStockTransactions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStockTransactions_CreatedByUserId",
                table: "LeadMaterialStockTransactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStockTransactions_LeadId_CreatedAt",
                table: "LeadMaterialStockTransactions",
                columns: new[] { "LeadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStockTransactions_LeadMaterialStockId",
                table: "LeadMaterialStockTransactions",
                column: "LeadMaterialStockId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStockTransactions_MaterialDeliveryId",
                table: "LeadMaterialStockTransactions",
                column: "MaterialDeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadMaterialStockTransactions_MaterialRequestId",
                table: "LeadMaterialStockTransactions",
                column: "MaterialRequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadMaterialStockTransactions");

            migrationBuilder.DropTable(
                name: "LeadMaterialStocks");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "MaterialDeliveryItems");
        }
    }
}
