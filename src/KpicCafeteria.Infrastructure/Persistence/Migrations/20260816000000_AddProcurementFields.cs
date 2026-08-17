using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KpicCafeteria.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 발주 관리 5단계 필드 추가.
    /// - ingredients: purchase_package_quantity / purchase_package_unit (판매 포장단위, nullable)
    /// - order_items: order_note (발주 비고, nullable)
    /// 기존 데이터를 삭제/초기화하지 않는다.
    /// </summary>
    public partial class AddProcurementFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "purchase_package_quantity",
                table: "ingredients",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "purchase_package_unit",
                table: "ingredients",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "order_note",
                table: "order_items",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "order_note",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "purchase_package_unit",
                table: "ingredients");

            migrationBuilder.DropColumn(
                name: "purchase_package_quantity",
                table: "ingredients");
        }
    }
}
