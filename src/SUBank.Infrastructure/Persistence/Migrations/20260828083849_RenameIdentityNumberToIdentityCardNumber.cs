using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SUBank.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameIdentityNumberToIdentityCardNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IdentityNumber",
                table: "CustomerProfiles",
                newName: "IdentityCardNumber");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerProfiles_IdentityNumber",
                table: "CustomerProfiles",
                newName: "IX_CustomerProfiles_IdentityCardNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IdentityCardNumber",
                table: "CustomerProfiles",
                newName: "IdentityNumber");

            migrationBuilder.RenameIndex(
                name: "IX_CustomerProfiles_IdentityCardNumber",
                table: "CustomerProfiles",
                newName: "IX_CustomerProfiles_IdentityNumber");
        }
    }
}
