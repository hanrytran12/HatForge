using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkPhotoType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "WorkPhotos",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "WorkPhotos");
        }
    }
}
