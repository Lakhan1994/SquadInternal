using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SquadInternal.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicableRolesToHoliday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicableRoles",
                table: "SquadHolidays",
                type: "nvarchar(max)",
                nullable: true);


        }

    }
}
