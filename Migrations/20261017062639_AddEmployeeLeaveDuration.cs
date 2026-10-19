using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeLeaveDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "EmployeeLeaves",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Full Day");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "EmployeeLeaves");
        }
    }
}
