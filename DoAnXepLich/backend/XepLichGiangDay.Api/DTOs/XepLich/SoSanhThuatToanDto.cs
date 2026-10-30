namespace XepLichGiangDay.Api.DTOs.XepLich;

public class SoSanhThuatToanDto
{
    public int MaHocKy { get; set; }
    public string TenHocKy { get; set; } = string.Empty;
    public int TongSoLop { get; set; }
    public int TongSoGiangVien { get; set; }
    public KetQuaXepLichDto KetQuaCpSat { get; set; } = new();
    public KetQuaXepLichDto KetQuaGa { get; set; } = new();
}

