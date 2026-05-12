using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionPayeeMemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Memo",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Payee",
                table: "transactions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Memo",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "Payee",
                table: "transactions");
        }
    }
}
