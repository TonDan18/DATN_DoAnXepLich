namespace XepLichGiangDay.Api.Services.ThuatToan.Genetic;

public class CauHinhGa
{
    public int KichThuocQuanThe { get; set; } = 50;
    public int SoTheHe { get; set; } = 100;
    public double TyLeLaiGhep { get; set; } = 0.8;
    public double TyLeDotBien { get; set; } = 0.05;
    public int KichThuocGiaiDau { get; set; } = 3;
    public int SoCaTheUuTu { get; set; } = 2;
    public int? RandomSeed { get; set; }
    public double ThoiGianChayToiDaGiay { get; set; } = 30.0;

    /// <summary>
    /// Chuẩn hóa và xác thực tính hợp lệ của tham số cấu hình.
    /// </summary>
    public void ChuanHoaVaXacThuc()
    {
        KichThuocQuanThe = Math.Clamp(KichThuocQuanThe, 4, 500);
        SoTheHe = Math.Clamp(SoTheHe, 1, 5000);
        TyLeLaiGhep = Math.Clamp(TyLeLaiGhep, 0.0, 1.0);
        TyLeDotBien = Math.Clamp(TyLeDotBien, 0.0, 1.0);
        KichThuocGiaiDau = Math.Clamp(KichThuocGiaiDau, 2, Math.Min(50, KichThuocQuanThe));
        SoCaTheUuTu = Math.Clamp(SoCaTheUuTu, 1, Math.Min(50, KichThuocQuanThe / 2));
        ThoiGianChayToiDaGiay = Math.Clamp(ThoiGianChayToiDaGiay <= 0 ? 30.0 : ThoiGianChayToiDaGiay, 1.0, 300.0);
    }
}

