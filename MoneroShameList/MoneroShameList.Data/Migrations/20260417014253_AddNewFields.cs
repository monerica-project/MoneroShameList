using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneroShameList.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MoneroAlternativeUrl",
                table: "Submissions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminComment",
                table: "ShameEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "ShameEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MoneroAlternativeUrl",
                table: "ShameEntries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MoneroAlternativeUrl",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AdminComment",
                table: "ShameEntries");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ShameEntries");

            migrationBuilder.DropColumn(
                name: "MoneroAlternativeUrl",
                table: "ShameEntries");
        }
    }
}
