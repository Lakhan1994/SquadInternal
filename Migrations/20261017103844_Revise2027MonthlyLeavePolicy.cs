using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class Revise2027MonthlyLeavePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AnnualLeavePolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CasualLeaveDays", "EarnedLeaveDays", "EarnedLeaveEnabled", "SickLeaveDays" },
                values: new object[] { 3m, 6m, true, 3m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AnnualLeavePolicies",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CasualLeaveDays", "EarnedLeaveDays", "EarnedLeaveEnabled", "SickLeaveDays" },
                values: new object[] { 6m, 0m, false, 6m });
        }
    }
}
