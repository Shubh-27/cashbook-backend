using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.model.Migrations
{
    /// <inheritdoc />
    public partial class AddImportProfileSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SampleJson",
                table: "ImportProfiles",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SampleJson",
                table: "ImportProfiles");
        }
    }
}
