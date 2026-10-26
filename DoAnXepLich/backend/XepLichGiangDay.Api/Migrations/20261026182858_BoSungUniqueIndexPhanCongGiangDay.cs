using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XepLichGiangDay.Api.Migrations
{
    /// <inheritdoc />
    public partial class BoSungUniqueIndexPhanCongGiangDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_phan_cong_giang_day_ma_lop_hoc_phan",
                table: "phan_cong_giang_day");

            migrationBuilder.CreateIndex(
                name: "IX_phan_cong_giang_day_ma_lop_hoc_phan",
                table: "phan_cong_giang_day",
                column: "ma_lop_hoc_phan",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_phan_cong_giang_day_ma_lop_hoc_phan",
                table: "phan_cong_giang_day");

            migrationBuilder.CreateIndex(
                name: "IX_phan_cong_giang_day_ma_lop_hoc_phan",
                table: "phan_cong_giang_day",
                column: "ma_lop_hoc_phan");
        }
    }
}
