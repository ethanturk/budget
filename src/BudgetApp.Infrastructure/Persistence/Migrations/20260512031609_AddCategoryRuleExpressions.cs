using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryRuleExpressions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_category_rules_CategoryId_MatchText",
                table: "category_rules");

            migrationBuilder.AlterColumn<string>(
                name: "MatchText",
                table: "category_rules",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DisplayText",
                table: "category_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RuleJson",
                table: "category_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE category_rules
                SET
                    "DisplayText" = 'Description contains "' || coalesce("MatchText", '') || '" OR Payee contains "' || coalesce("MatchText", '') || '" OR Memo contains "' || coalesce("MatchText", '') || '"',
                    "RuleJson" = json_build_object(
                        'root', json_build_object(
                            'type', 'group',
                            'operator', 'or',
                            'children', json_build_array(
                                json_build_object('type', 'condition', 'field', 'description', 'comparison', 'contains', 'value', coalesce("MatchText", '')),
                                json_build_object('type', 'condition', 'field', 'payee', 'comparison', 'contains', 'value', coalesce("MatchText", '')),
                                json_build_object('type', 'condition', 'field', 'memo', 'comparison', 'contains', 'value', coalesce("MatchText", ''))
                            )
                        )
                    )::text
                WHERE "RuleJson" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_category_rules_CategoryId_RuleJson",
                table: "category_rules",
                columns: new[] { "CategoryId", "RuleJson" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_category_rules_CategoryId_RuleJson",
                table: "category_rules");

            migrationBuilder.DropColumn(
                name: "DisplayText",
                table: "category_rules");

            migrationBuilder.DropColumn(
                name: "RuleJson",
                table: "category_rules");

            migrationBuilder.AlterColumn<string>(
                name: "MatchText",
                table: "category_rules",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_category_rules_CategoryId_MatchText",
                table: "category_rules",
                columns: new[] { "CategoryId", "MatchText" },
                unique: true);
        }
    }
}
