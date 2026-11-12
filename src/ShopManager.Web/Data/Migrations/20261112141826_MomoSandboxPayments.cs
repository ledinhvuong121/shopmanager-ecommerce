using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopManager.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MomoSandboxPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MomoPayUrl",
                table: "Orders",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MomoStarted",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "MomoTransactionId",
                table: "Orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PayWithMomo",
                table: "Orders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MomoTransactionId",
                table: "Orders",
                column: "MomoTransactionId",
                unique: true,
                filter: "[MomoTransactionId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_MomoTransactionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MomoPayUrl",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MomoStarted",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MomoTransactionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PayWithMomo",
                table: "Orders");
        }
    }
}
