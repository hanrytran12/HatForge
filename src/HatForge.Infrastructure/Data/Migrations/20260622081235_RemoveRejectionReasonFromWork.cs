using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRejectionReasonFromWork : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Works");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RejectionReason",
                table: "Works",
                type: "integer",
                nullable: true);
        }
    }
}
