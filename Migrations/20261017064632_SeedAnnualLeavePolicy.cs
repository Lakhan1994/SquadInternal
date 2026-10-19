using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class SeedAnnualLeavePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AnnualLeavePolicies",
                columns: new[] { "Id", "BonusConversionRate", "BonusExpirationMonths", "BonusMaxDaysPerEmployeeYear", "BonusMaxEligibleUnusedDays", "BonusRequiresApproval", "BonusUsesSeparateCategory", "BusinessTimeZoneId", "CarryForwardUnusedLeave", "CasualLeaveBonusEligible", "CasualLeaveDays", "ChangeReason", "EarnedLeaveDays", "EarnedLeaveEnabled", "EffectiveFrom", "ProrateNewJoiners", "SickLeaveBonusEligible", "SickLeaveDays", "UpdatedByUserId", "UpdatedOn" },
                values: new object[] { 1, 1m, null, null, null, false, false, "UTC", false, false, 6m, "Initial policy; unused leave bonus rules are not configured.", 0m, false, new DateTime(2027, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, 6m, null, new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AnnualLeavePolicies",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
