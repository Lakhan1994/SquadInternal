using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnualLeavePolicyAndBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnnualLeavePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CasualLeaveDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    SickLeaveDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    EarnedLeaveEnabled = table.Column<bool>(type: "bit", nullable: false),
                    EarnedLeaveDays = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ProrateNewJoiners = table.Column<bool>(type: "bit", nullable: false),
                    CasualLeaveBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    SickLeaveBonusEligible = table.Column<bool>(type: "bit", nullable: false),
                    BonusConversionRate = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    BonusMaxEligibleUnusedDays = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    BonusMaxDaysPerEmployeeYear = table.Column<decimal>(type: "decimal(9,2)", nullable: true),
                    BonusUsesSeparateCategory = table.Column<bool>(type: "bit", nullable: false),
                    BonusExpirationMonths = table.Column<int>(type: "int", nullable: true),
                    BonusRequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    CarryForwardUnusedLeave = table.Column<bool>(type: "bit", nullable: false),
                    BusinessTimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "int", nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualLeavePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnnualLeavePolicyAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnnualLeavePolicyId = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnualLeavePolicyAudits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeLeaveBalances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveYear = table.Column<int>(type: "int", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    AnnualEntitlement = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ProratedEntitlement = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    BonusCredits = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ApprovedConsumption = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    Adjustments = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ExpiredBalance = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    ClosingBalance = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLeaveBalances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLeaveBalances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaveBalanceTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveYear = table.Column<int>(type: "int", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Days = table.Column<decimal>(type: "decimal(9,2)", nullable: false),
                    EmployeeLeaveId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveBalanceTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRolloverExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LeaveYear = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedByUserId = table.Column<int>(type: "int", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRolloverExecutions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnnualLeavePolicies_EffectiveFrom",
                table: "AnnualLeavePolicies",
                column: "EffectiveFrom",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnnualLeavePolicyAudits_AnnualLeavePolicyId_ChangedOn",
                table: "AnnualLeavePolicyAudits",
                columns: new[] { "AnnualLeavePolicyId", "ChangedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLeaveBalances_EmployeeId_LeaveYear_LeaveType",
                table: "EmployeeLeaveBalances",
                columns: new[] { "EmployeeId", "LeaveYear", "LeaveType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalanceTransactions_EmployeeId_LeaveYear_LeaveType",
                table: "LeaveBalanceTransactions",
                columns: new[] { "EmployeeId", "LeaveYear", "LeaveType" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalanceTransactions_IdempotencyKey",
                table: "LeaveBalanceTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRolloverExecutions_LeaveYear",
                table: "LeaveRolloverExecutions",
                column: "LeaveYear",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnnualLeavePolicies");

            migrationBuilder.DropTable(
                name: "AnnualLeavePolicyAudits");

            migrationBuilder.DropTable(
                name: "EmployeeLeaveBalances");

            migrationBuilder.DropTable(
                name: "LeaveBalanceTransactions");

            migrationBuilder.DropTable(
                name: "LeaveRolloverExecutions");
        }
    }
}
