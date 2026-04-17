using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneroShameList.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugToShameEntry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the column as nullable first so existing rows don't violate NOT NULL
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "ShameEntries",
                type: "character varying(220)",
                maxLength: 220,
                nullable: true);

            // Backfill slugs from Name before applying the unique index
            migrationBuilder.Sql(@"
        UPDATE ""ShameEntries""
        SET ""Slug"" = LOWER(
            REGEXP_REPLACE(
                REGEXP_REPLACE(
                    REGEXP_REPLACE(TRIM(""Name""), '[^a-zA-Z0-9\s-]', '', 'g'),
                '\s+', '-', 'g'),
            '-+', '-', 'g'))
        WHERE ""Slug"" IS NULL OR ""Slug"" = '';
    ");

            // Now make it non-nullable and add the unique index
            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "ShameEntries",
                type: "character varying(220)",
                maxLength: 220,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(220)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShameEntries_Slug",
                table: "ShameEntries",
                column: "Slug",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShameEntries_Slug",
                table: "ShameEntries");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "ShameEntries");
        }
 
    }
}
