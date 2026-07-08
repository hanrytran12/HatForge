using HatForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HatForge.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260708170000_AddHatModelCode")]
    public partial class AddHatModelCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "HatModels",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"UPDATE ""HatModels"" SET ""Code"" = 'HAT-' || ""Id"" WHERE ""Code"" = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "HatModels",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldDefaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_HatModels_Code",
                table: "HatModels",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HatModels_Code",
                table: "HatModels");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "HatModels");
        }
    }
}
