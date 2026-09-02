using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SUBank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminCustomerSuspension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AdminSuspendedAtUtc",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminSuspendedByUserId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminSuspensionReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdminSuspended",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminSuspendedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AdminSuspendedByUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AdminSuspensionReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsAdminSuspended",
                table: "AspNetUsers");
        }
    }
}
