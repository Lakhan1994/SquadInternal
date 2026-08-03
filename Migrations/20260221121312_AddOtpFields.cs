using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'OtpExpiry' AND Object_ID = Object_ID(N'Users'))
                BEGIN
                    ALTER TABLE [Users] ADD [OtpExpiry] datetime2 NULL;
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'ResetOtp' AND Object_ID = Object_ID(N'Users'))
                BEGIN
                    ALTER TABLE [Users] ADD [ResetOtp] nvarchar(max) NULL;
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpExpiry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ResetOtp",
                table: "Users");
        }
    }
}
