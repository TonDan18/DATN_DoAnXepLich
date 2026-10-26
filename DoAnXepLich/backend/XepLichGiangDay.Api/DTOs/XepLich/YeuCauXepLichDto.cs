using System.ComponentModel.DataAnnotations;

namespace XepLichGiangDay.Api.DTOs.XepLich;

public class YeuCauXepLichDto
{
    [Required]
    public int MaHocKy { get; set; }

    public string ThuatToan { get; set; } = "GA";

    [Range(4, 500, ErrorMessage = "Kích thước quần thể phải từ 4 đến 500.")]
    public int? KichThuocQuanThe { get; set; }

    [Range(1, 5000, ErrorMessage = "Số thế hệ phải từ 1 đến 5000.")]
    public int? SoTheHe { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "Tỷ lệ lai ghép phải từ 0.0 đến 1.0.")]
    public double? TyLeLaiGhep { get; set; }

    [Range(0.0, 1.0, ErrorMessage = "Tỷ lệ đột biến phải từ 0.0 đến 1.0.")]
    public double? TyLeDotBien { get; set; }

    public int? RandomSeed { get; set; }

    [Range(2, 50, ErrorMessage = "Kích thước giải đấu phải từ 2 đến 50.")]
    public int? KichThuocGiaiDau { get; set; }

    [Range(1, 50, ErrorMessage = "Số cá thể ưu tú phải từ 1 đến 50.")]
    public int? SoCaTheUuTu { get; set; }

    // Tham số cấu hình tối ưu cho CP-SAT (Google OR-Tools)
    [Range(1.0, 300.0, ErrorMessage = "Thời gian chạy tối đa phải từ 1.0 đến 300.0 giây.")]
    public double? ThoiGianChayToiDa { get; set; } = 30.0;

    [Range(1, 32, ErrorMessage = "Số lượng worker phải từ 1 đến 32.")]
    public int? SoLuongWorker { get; set; } = 1;

    [Range(0.0, 1.0, ErrorMessage = "Khoảng cách tối ưu GAP phải từ 0.0 đến 1.0.")]
    public double? KhoangCachToiUuGap { get; set; } = 0.01;
}

