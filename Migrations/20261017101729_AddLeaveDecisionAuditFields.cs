using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveDecisionAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "EmployeeLeaves",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledOn",
                table: "EmployeeLeaves",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionOn",
                table: "EmployeeLeaves",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "EmployeeLeaves",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveBalanceTransactions_EmployeeLeaveId",
                table: "LeaveBalanceTransactions",
                column: "EmployeeLeaveId");

            migrationBuilder.AddForeignKey(
                name: "FK_AnnualLeavePolicyAudits_AnnualLeavePolicies_AnnualLeavePolicyId",
                table: "AnnualLeavePolicyAudits",
                column: "AnnualLeavePolicyId",
                principalTable: "AnnualLeavePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveBalanceTransactions_EmployeeLeaves_EmployeeLeaveId",
                table: "LeaveBalanceTransactions",
                column: "EmployeeLeaveId",
                principalTable: "EmployeeLeaves",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveBalanceTransactions_Employees_EmployeeId",
                table: "LeaveBalanceTransactions",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnnualLeavePolicyAudits_AnnualLeavePolicies_AnnualLeavePolicyId",
                table: "AnnualLeavePolicyAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveBalanceTransactions_EmployeeLeaves_EmployeeLeaveId",
                table: "LeaveBalanceTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveBalanceTransactions_Employees_EmployeeId",
                table: "LeaveBalanceTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LeaveBalanceTransactions_EmployeeLeaveId",
                table: "LeaveBalanceTransactions");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "EmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "CancelledOn",
                table: "EmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "DecisionOn",
                table: "EmployeeLeaves");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "EmployeeLeaves");
        }
    }
}
