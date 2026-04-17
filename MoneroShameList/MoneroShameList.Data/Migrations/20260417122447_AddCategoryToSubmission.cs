using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneroShameList.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Submissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Submissions");
        }
    }
}
