using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "EmployeeLeaves",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "EmployeeLeaves");
        }
    }
}
