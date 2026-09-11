using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SUBank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressChangeWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AddressChangeRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestNo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    CustomerProfileId = table.Column<long>(type: "bigint", nullable: false),
                    PermanentAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TemporaryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AddressChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AddressChangeRequests_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AddressChangeRequests_CustomerProfiles_CustomerProfileId",
                        column: x => x.CustomerProfileId,
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AddressChangeRequests_CustomerProfileId_Status",
                table: "AddressChangeRequests",
                columns: new[] { "CustomerProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AddressChangeRequests_DecidedByUserId",
                table: "AddressChangeRequests",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AddressChangeRequests_RequestNo",
                table: "AddressChangeRequests",
                column: "RequestNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AddressChangeRequests");
        }
    }
}
