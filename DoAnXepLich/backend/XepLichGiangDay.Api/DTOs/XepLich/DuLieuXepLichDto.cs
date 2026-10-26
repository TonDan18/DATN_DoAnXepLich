using System.ComponentModel.DataAnnotations;

namespace XepLichGiangDay.Api.DTOs.XepLich;

public class DuLieuXepLichDto
{
    public int MaHocKy { get; set; }
    public string ThuatToan { get; set; } = string.Empty;

    [Range(4, 500)]
    public int? KichThuocQuanThe { get; set; }

    [Range(1, 5000)]
    public int? SoTheHe { get; set; }

    [Range(0.0, 1.0)]
    public double? TyLeLaiGhep { get; set; }

    [Range(0.0, 1.0)]
    public double? TyLeDotBien { get; set; }

    public int? RandomSeed { get; set; }

    [Range(2, 50)]
    public int? KichThuocGiaiDau { get; set; }

    [Range(1, 50)]
    public int? SoCaTheUuTu { get; set; }

    // Tham số cấu hình tối ưu cho CP-SAT (Google OR-Tools)
    [Range(1.0, 300.0)]
    public double? ThoiGianChayToiDa { get; set; } = 30.0;

    [Range(1, 32)]
    public int? SoLuongWorker { get; set; } = 1; // 1 luồng đảm bảo tính tất định 100%

    [Range(0.0, 1.0)]
    public double? KhoangCachToiUuGap { get; set; } = 0.01;
}

