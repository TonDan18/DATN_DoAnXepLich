using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.Models.Entities;

namespace XepLichGiangDay.Api.Services.RangBuoc;

/// <summary>
/// Ngữ cảnh dữ liệu được index sẵn để đánh giá phương án siêu tốc trong vòng lặp tiến hóa GA.
/// </summary>
public class BoiCanhDanhGia
{
    public int MaHocKy { get; set; }
    public List<LopHocPhan> DanhSachLop { get; set; } = [];
    public Dictionary<int, LopHocPhan> LopTheoMa { get; set; } = [];
    public Dictionary<int, List<ChiTietThoiKhoaBieu>> ChiTietTheoLop { get; set; } = [];
    public Dictionary<int, GiangVien> GiangVienTheoMa { get; set; } = [];
    public Dictionary<int, HashSet<int>> NangLucTheoGv { get; set; } = []; // MaGiangVien -> Set<MaHocPhan>
    public Dictionary<int, List<LichBanGiangVien>> LichBanTheoGv { get; set; } = []; // MaGiangVien -> List<LichBan>
    public Dictionary<int, List<NguyenVongGiangVien>> NguyenVongTheoGv { get; set; } = []; // MaGiangVien -> List<NguyenVong>
    public Dictionary<int, DinhMucGiangVien> DinhMucTheoGv { get; set; } = []; // MaGiangVien -> DinhMuc
    public Dictionary<string, double> TrongSoRangBuoc { get; set; } = [];

    // Trọng số các tiêu chí (lấy từ cấu hình bảng rang_buoc_hoc_ky hoặc mặc định chuẩn)
    public double TrongSoChuyenMon => LayTrongSo(["chuyên môn", "Sai chuyên môn"], 1000.0);
    public double TrongSoTrungLich => LayTrongSo(["trùng lịch", "Trùng lịch dạy"], 1000.0);
    public double TrongSoLichBan => LayTrongSo(["lịch bận", "Vi phạm lịch bận cố định"], 1000.0);
    public double TrongSoPhuLop => LayTrongSo(["phủ kín", "Phủ kín các lớp học phần"], 1000.0);
    public double TrongSoCanBangTai => LayTrongSo(["định mức", "cân bằng", "Lệch định mức giờ dạy"], 15.0);
    public double TrongSoNguyenVong => LayTrongSo(["nguyện vọng", "thời gian", "Không thỏa mãn nguyện vọng"], 10.0);
    public double TrongSoGomLich => LayTrongSo(["gom lịch", "tiết trống", "rải lịch", "Rải lịch dạy trong tuần"], 8.0);
    public double TrongSoCoHuu => LayTrongSo(["cơ hữu", "Ưu tiên giảng viên cơ hữu"], 5.0);

    private double LayTrongSo(string[] tuKhoa, double macDinh)
    {
        foreach (var (k, v) in TrongSoRangBuoc)
        {
            foreach (var tk in tuKhoa)
            {
                if (k.Contains(tk, StringComparison.OrdinalIgnoreCase) || tk.Contains(k, StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
            }
        }
        return macDinh;
    }
}

public class KetQuaDanhGiaPhuongAn
{
    public double TongDiemPhat { get; set; }
    public double DoThichNghi { get; set; } // Fitness = 1.0 / (1.0 + TongDiemPhat)
    public double DiemPhatRangBuocCung { get; set; }
    public double DiemPhatNguyenVong { get; set; }
    public double DiemPhatCanBangTai { get; set; }
    public double DiemPhatGomLich { get; set; }
    public int SoLopChuaPhanCong { get; set; }
    public int SoViPhamChuyenMon { get; set; }
    public int SoViPhamTrungLich { get; set; }
    public int SoViPhamLichBan { get; set; }
    public int SoViPhamCung => SoLopChuaPhanCong + SoViPhamChuyenMon + SoViPhamTrungLich + SoViPhamLichBan;
    public double TongDiemPhatMem => DiemPhatNguyenVong + DiemPhatCanBangTai + DiemPhatGomLich;
    public bool HopLe => SoViPhamCung == 0;
    public List<string> DanhSachViPham { get; set; } = [];
    public List<string> DanhSachCanhBaoMem { get; set; } = [];
}


public class BoDanhGiaPhuongAn
{
    private readonly AppDbContext? _context;
    private readonly BoKiemTraRangBuoc _boKiemTra;

    public BoDanhGiaPhuongAn()
    {
        _boKiemTra = new BoKiemTraRangBuoc();
    }

    public BoDanhGiaPhuongAn(AppDbContext context, BoKiemTraRangBuoc boKiemTra)
    {
        _context = context;
        _boKiemTra = boKiemTra;
    }

    /// <summary>
    /// Đánh giá toàn diện phương án phân công (tính điểm phạt ràng buộc cứng + mềm, xuất ra Fitness).
    /// </summary>
    public KetQuaDanhGiaPhuongAn DanhGia(IDictionary<int, int?> phanCong, BoiCanhDanhGia boiCanh)
    {
        var ketQua = new KetQuaDanhGiaPhuongAn();

        // 1. Kiểm tra RÀNG BUỘC CỨNG (Hard Constraints)
        // 1.1 Phủ kín lớp
        foreach (var lop in boiCanh.DanhSachLop)
        {
            if (!phanCong.TryGetValue(lop.MaLopHocPhan, out var maGv) || !maGv.HasValue || maGv.Value <= 0)
            {
                ketQua.SoLopChuaPhanCong++;
                ketQua.DiemPhatRangBuocCung += boiCanh.TrongSoPhuLop;
                ketQua.DanhSachViPham.Add($"Lớp [{lop.MaLopHocPhanTruong}] chưa được phân công giảng viên.");
            }
        }

        // 1.2 Đúng chuyên môn
        foreach (var (maLop, maGvOpt) in phanCong)
        {
            if (!maGvOpt.HasValue || maGvOpt.Value <= 0) continue;
            int maGv = maGvOpt.Value;

            if (boiCanh.LopTheoMa.TryGetValue(maLop, out var lop))
            {
                bool dungChuyenMon = boiCanh.NangLucTheoGv.TryGetValue(maGv, out var setHp) && setHp.Contains(lop.MaHocPhan);
                if (!dungChuyenMon)
                {
                    ketQua.SoViPhamChuyenMon++;
                    ketQua.DiemPhatRangBuocCung += boiCanh.TrongSoChuyenMon;
                    string tenGv = boiCanh.GiangVienTheoMa.TryGetValue(maGv, out var gv) ? gv.HoTen : $"GV #{maGv}";
                    ketQua.DanhSachViPham.Add($"{tenGv} không có chuyên môn dạy lớp [{lop.MaLopHocPhanTruong}].");
                }
            }
        }

        // Nhóm các lớp theo từng giảng viên được phân công
        var lopTheoGv = phanCong
            .Where(x => x.Value.HasValue && x.Value.Value > 0)
            .GroupBy(x => x.Value!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Key).ToList());

        // 1.3 Trùng lịch và vướng lịch bận cố định
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            string tenGv = boiCanh.GiangVienTheoMa.TryGetValue(maGv, out var gv) ? gv.HoTen : $"GV #{maGv}";

            var cacBuoiHocCuaGv = new List<ChiTietThoiKhoaBieu>();
            foreach (var maLop in dsLopCuaGv)
            {
                if (boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsCt))
                {
                    cacBuoiHocCuaGv.AddRange(dsCt);
                }
            }

            // Kiểm tra trùng lịch giữa 2 lớp khác nhau
            for (int i = 0; i < cacBuoiHocCuaGv.Count; i++)
            {
                for (int j = i + 1; j < cacBuoiHocCuaGv.Count; j++)
                {
                    var ct1 = cacBuoiHocCuaGv[i];
                    var ct2 = cacBuoiHocCuaGv[j];

                    if (ct1.MaLopHocPhan != ct2.MaLopHocPhan && _boKiemTra.CoTrungLich(
                        ct1.Thu, ct1.TietBatDau, ct1.TietKetThuc, ct1.TuTuan, ct1.DenTuan,
                        ct2.Thu, ct2.TietBatDau, ct2.TietKetThuc, ct2.TuTuan, ct2.DenTuan))
                    {
                        ketQua.SoViPhamTrungLich++;
                        ketQua.DiemPhatRangBuocCung += boiCanh.TrongSoTrungLich;
                        ketQua.DanhSachViPham.Add($"{tenGv} bị trùng giờ dạy giữa 2 lớp vào Thứ {ct1.Thu}, tiết {Math.Max(ct1.TietBatDau, ct2.TietBatDau)}-{Math.Min(ct1.TietKetThuc, ct2.TietKetThuc)}.");
                    }
                }

                // Kiểm tra vướng lịch bận cố định
                var ct = cacBuoiHocCuaGv[i];
                if (boiCanh.LichBanTheoGv.TryGetValue(maGv, out var dsLb))
                {
                    if (dsLb.Any(lb => lb.Thu == ct.Thu && Math.Max(lb.TietBatDau, ct.TietBatDau) <= Math.Min(lb.TietKetThuc, ct.TietKetThuc)))
                    {
                        ketQua.SoViPhamLichBan++;
                        ketQua.DiemPhatRangBuocCung += boiCanh.TrongSoLichBan;
                        ketQua.DanhSachViPham.Add($"{tenGv} bị vướng lịch bận cố định vào Thứ {ct.Thu}, tiết {ct.TietBatDau}-{ct.TietKetThuc}.");
                    }
                }
            }
        }

        // 2. Kiểm tra RÀNG BUỘC MỀM (Soft Constraints)
        // 2.1 Điểm nguyện vọng thời gian
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            foreach (var maLop in dsLopCuaGv)
            {
                var diemNv = TinhDiemNguyenVong(maGv, maLop, boiCanh, out var canhBaoNv);
                ketQua.DiemPhatNguyenVong += diemNv;
                if (!string.IsNullOrEmpty(canhBaoNv))
                {
                    ketQua.DanhSachCanhBaoMem.Add(canhBaoNv);
                }
            }
        }

        // 2.2 Điểm cân bằng tải & 2.3 Điểm gom lịch
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            var diemCbt = TinhDiemCanBangTai(maGv, dsLopCuaGv, boiCanh, out var canhBaoCbt);
            ketQua.DiemPhatCanBangTai += diemCbt;
            if (!string.IsNullOrEmpty(canhBaoCbt))
            {
                ketQua.DanhSachCanhBaoMem.Add(canhBaoCbt);
            }

            var diemGl = TinhDiemGomLich(maGv, dsLopCuaGv, boiCanh, out var canhBaoGl);
            ketQua.DiemPhatGomLich += diemGl;
            if (!string.IsNullOrEmpty(canhBaoGl))
            {
                ketQua.DanhSachCanhBaoMem.Add(canhBaoGl);
            }

            // 2.4 Điểm hồi phục sức khỏe (Mục 2.3 trong đặc tả Nhà trường: >= 10h giữa ca tối và ca sáng hôm sau)
            var diemHp = TinhDiemHoiPhucSucKhoe(maGv, dsLopCuaGv, boiCanh, out var canhBaoHp);
            ketQua.DiemPhatNguyenVong += diemHp;
            if (!string.IsNullOrEmpty(canhBaoHp))
            {
                ketQua.DanhSachCanhBaoMem.Add(canhBaoHp);
            }
        }

        // 3. TỔNG HỢP ĐIỂM PHẠT VÀ ĐỘ THÍCH NGHI (FITNESS PHÂN TẦNG)
        ketQua.TongDiemPhat = ketQua.DiemPhatRangBuocCung + ketQua.TongDiemPhatMem;

        // Phân tầng nghiêm ngặt (Lexicographic / Barrier Penalty):
        // Nếu vi phạm ràng buộc cứng (SoViPhamCung > 0): Fitness < 0.001
        // Nếu thỏa mãn 100% ràng buộc cứng (SoViPhamCung == 0): Fitness >= 1.0
        if (ketQua.SoViPhamCung > 0)
        {
            ketQua.DoThichNghi = 1.0 / (1000.0 + (ketQua.SoViPhamCung * 100.0) + (ketQua.TongDiemPhatMem * 0.001));
        }
        else
        {
            ketQua.DoThichNghi = 1.0 + (1000.0 / (1.0 + ketQua.TongDiemPhatMem));
        }

        return ketQua;
    }


    /// <summary>
    /// Tính điểm phạt nguyện vọng: Ưu tiên khung giờ, ca sáng/chiều, tránh ca tối, tránh cuối tuần, ưu tiên GV cơ hữu.
    /// </summary>
    public double TinhDiemNguyenVong(int maGiangVien, int maLopHocPhan, BoiCanhDanhGia boiCanh, out string canhBao)
    {
        canhBao = string.Empty;
        if (maGiangVien <= 0) return 0;

        double diemPhat = 0;
        boiCanh.ChiTietTheoLop.TryGetValue(maLopHocPhan, out var dsChiTiet);
        dsChiTiet ??= [];

        string tenGv = boiCanh.GiangVienTheoMa.TryGetValue(maGiangVien, out var gv) ? gv.HoTen : $"GV #{maGiangVien}";
        string maLopTruong = boiCanh.LopTheoMa.TryGetValue(maLopHocPhan, out var lop) ? lop.MaLopHocPhanTruong : $"Lớp #{maLopHocPhan}";

        if (boiCanh.NguyenVongTheoGv.TryGetValue(maGiangVien, out var dsNv) && dsNv.Count > 0)
        {
            foreach (var ct in dsChiTiet)
            {
                foreach (var nv in dsNv)
                {
                    var maRb = nv.LoaiNguyenVong?.Trim().ToUpperInvariant() ?? "";
                    double trongSoNv = nv.TrongSoPhat > 0 ? nv.TrongSoPhat : boiCanh.TrongSoNguyenVong;

                    // SC01 hoặc TranhDay theo khung giờ cụ thể
                    if (maRb == "SC01" || maRb == "TRANHDAY")
                    {
                        if (nv.Thu.HasValue && nv.Thu.Value == ct.Thu)
                        {
                            int nvTbd = nv.TietBatDau ?? 1;
                            int nvTkt = nv.TietKetThuc ?? 12;
                            if (Math.Max(nvTbd, ct.TietBatDau) <= Math.Min(nvTkt, ct.TietKetThuc))
                            {
                                diemPhat += trongSoNv;
                                canhBao = $"{tenGv} bị phân vào khung giờ tránh dạy (Thứ {ct.Thu}, tiết {ct.TietBatDau}-{ct.TietKetThuc}) ở lớp [{maLopTruong}].";
                            }
                        }
                    }
                    // SC02: Tránh ca tối (Tiết 10-12)
                    else if (maRb == "SC02")
                    {
                        if (ct.TietKetThuc >= 10)
                        {
                            diemPhat += trongSoNv * 1.5;
                            canhBao = $"{tenGv} có nguyện vọng tránh ca tối nhưng lớp [{maLopTruong}] có tiết đến {ct.TietKetThuc}.";
                        }
                    }
                    // SC03: Tránh dạy thứ 7 / Chủ nhật
                    else if (maRb == "SC03")
                    {
                        if (ct.Thu >= 7)
                        {
                            diemPhat += trongSoNv * 2.0;
                            canhBao = $"{tenGv} có nguyện vọng tránh dạy cuối tuần nhưng lớp [{maLopTruong}] xếp vào Thứ {ct.Thu}.";
                        }
                    }
                    // SC04 hoặc ThichDay sáng (Tiết 1-6)
                    else if (maRb == "SC04" || (maRb == "THICHDAY" && (nv.TietKetThuc ?? 0) <= 6 && !nv.Thu.HasValue))
                    {
                        if (ct.TietBatDau >= 7)
                        {
                            diemPhat += trongSoNv * 0.8;
                            canhBao = $"{tenGv} ưu tiên dạy ca sáng nhưng lớp [{maLopTruong}] học ca chiều (tiết {ct.TietBatDau}-{ct.TietKetThuc}).";
                        }
                    }
                    // SC05: Ưu tiên dạy chiều (Tiết 7-12)
                    else if (maRb == "SC05" || (maRb == "THICHDAY" && (nv.TietBatDau ?? 0) >= 7 && !nv.Thu.HasValue))
                    {
                        if (ct.TietKetThuc <= 6)
                        {
                            diemPhat += trongSoNv * 0.8;
                            canhBao = $"{tenGv} ưu tiên dạy ca chiều nhưng lớp [{maLopTruong}] học ca sáng (tiết {ct.TietBatDau}-{ct.TietKetThuc}).";
                        }
                    }
                    // ThichDay theo khung giờ cụ thể
                    else if (maRb == "THICHDAY" && nv.Thu.HasValue)
                    {
                        if (nv.Thu.Value == ct.Thu)
                        {
                            int nvTbd = nv.TietBatDau ?? 1;
                            int nvTkt = nv.TietKetThuc ?? 12;
                            if (Math.Max(nvTbd, ct.TietBatDau) <= Math.Min(nvTkt, ct.TietKetThuc))
                            {
                                // Đúng nguyện vọng mong muốn -> Giảm điểm phạt nếu có
                                diemPhat = Math.Max(0, diemPhat - 2.0);
                            }
                        }
                    }
                }
            }
        }

        // Ưu tiên GV cơ hữu đủ giờ trước khi dùng GV thỉnh giảng
        if (boiCanh.GiangVienTheoMa.TryGetValue(maGiangVien, out var giangVien))
        {
            bool isThinhGiang = (giangVien.ChucDanh != null && giangVien.ChucDanh.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase)) ||
                                giangVien.HoTen.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase);
            if (isThinhGiang)
            {
                diemPhat += 1.0 * boiCanh.TrongSoCoHuu;
            }
        }

        return diemPhat;
    }

    public double TinhDiemNguyenVong(int maGiangVien, int maLopHocPhan, BoiCanhDanhGia boiCanh)
    {
        return TinhDiemNguyenVong(maGiangVien, maLopHocPhan, boiCanh, out _);
    }

    /// <summary>
    /// Tính điểm phạt cân bằng tải: So khớp tải tuần thực tế (xét theo từng tuần học tu_tuan -> den_tuan) với định mức trần 25 tiết/tuần của Nhà trường.
    /// </summary>
    public double TinhDiemCanBangTai(int maGiangVien, List<int> dsMaLopPhanCong, BoiCanhDanhGia boiCanh, out string canhBao)
    {
        canhBao = string.Empty;
        if (maGiangVien <= 0 || dsMaLopPhanCong == null) return 0;

        var cacBuoiHoc = new List<ChiTietThoiKhoaBieu>();
        foreach (var maLop in dsMaLopPhanCong)
        {
            if (boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsCt))
            {
                cacBuoiHoc.AddRange(dsCt);
            }
        }

        int maxTietTuan = 0; // Tuần cao điểm nhất (Peak week load)
        int tongTietToanKy = 0;
        int soTuanCoDay = 0;

        if (cacBuoiHoc.Count > 0)
        {
            // Xác định khoảng tuần học của các lớp
            int minTuanHk = cacBuoiHoc.Min(b => b.TuTuan > 0 ? b.TuTuan : 1);
            int maxTuanHk = cacBuoiHoc.Max(b => b.DenTuan > 0 ? b.DenTuan : 20);
            if (minTuanHk <= 0) minTuanHk = 1;
            if (maxTuanHk < minTuanHk) maxTuanHk = minTuanHk;

            // Tính số tiết thực tế theo từng tuần cụ thể (không cộng dồn các lớp học khác tuần)
            for (int w = minTuanHk; w <= maxTuanHk; w++)
            {
                int soTietTuanW = 0;
                foreach (var b in cacBuoiHoc)
                {
                    if (w >= b.TuTuan && w <= b.DenTuan)
                    {
                        soTietTuanW += (b.TietKetThuc >= b.TietBatDau ? b.TietKetThuc - b.TietBatDau + 1 : 0);
                    }
                }

                if (soTietTuanW > 0)
                {
                    soTuanCoDay++;
                    tongTietToanKy += soTietTuanW;
                    if (soTietTuanW > maxTietTuan)
                    {
                        maxTietTuan = soTietTuanW;
                    }
                }
            }
        }

        // Định mức chuẩn theo quy chế Nhà trường (Mục 1.4: Không quá 25 tiết/tuần đối với GV cơ hữu, 16 đối với thỉnh giảng)
        var (minTiet, maxTiet) = LayDinhMucTuan(maGiangVien, boiCanh);
        boiCanh.GiangVienTheoMa.TryGetValue(maGiangVien, out var gv);

        string tenGv = gv?.HoTen ?? $"GV #{maGiangVien}";
        double diemPhat = 0;
        double taiTrungBinhTuan = soTuanCoDay > 0 ? (double)tongTietToanKy / soTuanCoDay : 0;

        if (maxTietTuan > maxTiet)
        {
            diemPhat = (maxTietTuan - maxTiet) * boiCanh.TrongSoCanBangTai * 1.5;
            canhBao = $"{tenGv} bị quá tải ({maxTietTuan}/{maxTiet} tiết/tuần ở tuần cao điểm).";
        }
        else if (taiTrungBinhTuan < minTiet)
        {
            diemPhat = (minTiet - taiTrungBinhTuan) * boiCanh.TrongSoCanBangTai;
            canhBao = $"{tenGv} bị thiếu tải ({Math.Round(taiTrungBinhTuan, 1)}/{minTiet} tiết/tuần trung bình).";
        }

        return diemPhat;
    }

    public double TinhDiemCanBangTai(int maGiangVien, List<int> dsMaLopPhanCong, BoiCanhDanhGia boiCanh)
    {
        return TinhDiemCanBangTai(maGiangVien, dsMaLopPhanCong, boiCanh, out _);
    }

    /// <summary>
    /// Chuẩn hóa định mức tuần của giảng viên (chuyển đổi định mức học kỳ > 30 về định mức tuần / 15).
    /// </summary>
    public static (int MinTiet, int MaxTiet) LayDinhMucTuan(int maGiangVien, BoiCanhDanhGia boiCanh)
    {
        bool isThinhGiang = boiCanh.GiangVienTheoMa.TryGetValue(maGiangVien, out var gv) &&
            ((gv.ChucDanh != null && gv.ChucDanh.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase)) ||
             gv.HoTen.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase));

        int minTiet = isThinhGiang ? 3 : 6;
        int maxTiet = isThinhGiang ? 16 : 25;

        if (boiCanh.DinhMucTheoGv.TryGetValue(maGiangVien, out var dm))
        {
            if (dm.SoTietToiThieu > 0)
            {
                minTiet = dm.SoTietToiThieu > 30 ? (int)Math.Ceiling(dm.SoTietToiThieu / 15.0) : dm.SoTietToiThieu;
            }
            if (dm.SoTietToiDa > 0)
            {
                maxTiet = (dm.SoTietToiDa > 0 && dm.SoTietToiDa <= (isThinhGiang ? 16 : 25)) ? dm.SoTietToiDa : (isThinhGiang ? 16 : 25);
            }
        }

        return (minTiet, maxTiet);
    }

    /// <summary>
    /// Tính điểm phạt gom lịch: Phạt tiết trống giữa ca trong cùng ngày có giao tuần, phạt rải lịch nhiều ngày trong một tuần thực tế.
    /// </summary>
    public double TinhDiemGomLich(int maGiangVien, List<int> dsMaLopPhanCong, BoiCanhDanhGia boiCanh, out string canhBao)
    {
        canhBao = string.Empty;
        if (maGiangVien <= 0 || dsMaLopPhanCong.Count == 0) return 0;

        var cacBuoiHoc = new List<ChiTietThoiKhoaBieu>();
        foreach (var maLop in dsMaLopPhanCong)
        {
            if (boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsCt))
            {
                cacBuoiHoc.AddRange(dsCt);
            }
        }

        if (cacBuoiHoc.Count == 0) return 0;

        string tenGv = boiCanh.GiangVienTheoMa.TryGetValue(maGiangVien, out var gv) ? gv.HoTen : $"GV #{maGiangVien}";
        double diemPhat = 0;

        bool coNguyenVongGomLich = boiCanh.NguyenVongTheoGv.TryGetValue(maGiangVien, out var dsNv) &&
            dsNv.Any(nv => nv.LoaiNguyenVong?.Trim().Equals("SC07", StringComparison.OrdinalIgnoreCase) == true);
        double heSoGomLich = coNguyenVongGomLich ? 1.5 : 1.0;

        // 1. Phạt tiết trống giữa ca trong cùng một ngày (chỉ tính nếu 2 buổi học có tuần giao nhau thực tế)
        var buoiTheoThu = cacBuoiHoc.GroupBy(x => x.Thu);
        foreach (var groupThu in buoiTheoThu)
        {
            var dsSapXep = groupThu.OrderBy(x => x.TietBatDau).ToList();

            for (int i = 0; i < dsSapXep.Count; i++)
            {
                var b1 = dsSapXep[i];

                if (i < dsSapXep.Count - 1)
                {
                    var b2 = dsSapXep[i + 1];
                    bool giaoTuan = Math.Max(b1.TuTuan, b2.TuTuan) <= Math.Min(b1.DenTuan, b2.DenTuan);
                    if (giaoTuan)
                    {
                        int gap = b2.TietBatDau - b1.TietKetThuc - 1;
                        if (gap > 0)
                        {
                            diemPhat += gap * (boiCanh.TrongSoGomLich * 0.5 * heSoGomLich);
                            if (string.IsNullOrEmpty(canhBao))
                            {
                                canhBao = $"{tenGv} có {gap} tiết trống giữa ca vào Thứ {b1.Thu}.";
                            }
                        }
                    }
                }
            }
        }

        // 2. Phạt nếu số ngày dạy trong MỘT TUẦN THỰC TẾ quá nhiều (rải rác > 3 ngày/tuần ở cùng một tuần)
        int minTuanHk = cacBuoiHoc.Min(b => b.TuTuan > 0 ? b.TuTuan : 1);
        int maxTuanHk = cacBuoiHoc.Max(b => b.DenTuan > 0 ? b.DenTuan : 20);
        int maxNgayDayTrongTuan = 0;

        for (int w = minTuanHk; w <= maxTuanHk; w++)
        {
            var dsThuTrongTuan = new HashSet<int>();
            int tongTietTuanW = 0;
            foreach (var b in cacBuoiHoc)
            {
                if (w >= b.TuTuan && w <= b.DenTuan)
                {
                    dsThuTrongTuan.Add(b.Thu);
                    tongTietTuanW += (b.TietKetThuc >= b.TietBatDau ? b.TietKetThuc - b.TietBatDau + 1 : 0);
                }
            }

            if (dsThuTrongTuan.Count > maxNgayDayTrongTuan)
            {
                maxNgayDayTrongTuan = dsThuTrongTuan.Count;
            }

            // Phạt nếu 1 ngày dạy quá nhiều tiết (> 6 tiết/ngày trong cùng tuần w)
            if (tongTietTuanW > 6 && dsThuTrongTuan.Count == 1)
            {
                diemPhat += (tongTietTuanW - 6) * (boiCanh.TrongSoGomLich * 0.6);
            }
        }

        int nguongNgayToiDa = 3; // Tiêu chuẩn gom lịch: tối đa 3 ngày/tuần
        if (maxNgayDayTrongTuan > nguongNgayToiDa)
        {
            int ngayLech = maxNgayDayTrongTuan - nguongNgayToiDa;
            diemPhat += ngayLech * (boiCanh.TrongSoGomLich * heSoGomLich);
            if (string.IsNullOrEmpty(canhBao))
            {
                canhBao = $"{tenGv} bị rải lịch dạy ({maxNgayDayTrongTuan} ngày/tuần ở tuần cao điểm).";
            }
        }

        return diemPhat;
    }

    public double TinhDiemGomLich(int maGiangVien, List<int> dsMaLopPhanCong, BoiCanhDanhGia boiCanh)
    {
        return TinhDiemGomLich(maGiangVien, dsMaLopPhanCong, boiCanh, out _);
    }

    /// <summary>
    /// Tính điểm phạt vi phạm thời gian hồi phục sức khỏe (< 10 tiếng giữa ca tối hôm trước kết thúc 21h và ca sáng hôm sau bắt đầu 7h).
    /// Tuân thủ Mục 2.3 trong Đặc tả Ràng buộc của Nhà trường.
    /// </summary>
    public double TinhDiemHoiPhucSucKhoe(int maGiangVien, List<int> dsMaLopPhanCong, BoiCanhDanhGia boiCanh, out string canhBao)
    {
        canhBao = string.Empty;
        if (maGiangVien <= 0 || dsMaLopPhanCong.Count <= 1) return 0;

        var cacBuoiHoc = new List<ChiTietThoiKhoaBieu>();
        foreach (var maLop in dsMaLopPhanCong)
        {
            if (boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsCt))
            {
                cacBuoiHoc.AddRange(dsCt);
            }
        }
        if (cacBuoiHoc.Count <= 1) return 0;

        double diemPhat = 0;
        string tenGv = boiCanh.GiangVienTheoMa.TryGetValue(maGiangVien, out var gv) ? gv.HoTen : $"GV #{maGiangVien}";

        for (int i = 0; i < cacBuoiHoc.Count; i++)
        {
            for (int j = 0; j < cacBuoiHoc.Count; j++)
            {
                if (i == j) continue;
                var bt = cacBuoiHoc[i]; // Ca tối hôm trước
                var bs = cacBuoiHoc[j]; // Ca sáng hôm sau

                // Tiết kết thúc >= 10 (21h), ca sáng hôm sau: Thu == bt.Thu + 1, bắt đầu tiết <= 2 (7h-7h50)
                if (bt.TietKetThuc >= 10 && bs.Thu == bt.Thu + 1 && bs.TietBatDau <= 2)
                {
                    // Chỉ phạt nếu 2 buổi học có tuần giao nhau thực tế
                    bool giaoTuan = Math.Max(bt.TuTuan, bs.TuTuan) <= Math.Min(bt.DenTuan, bs.DenTuan);
                    if (giaoTuan)
                    {
                        diemPhat += boiCanh.TrongSoNguyenVong * 1.5;
                        if (string.IsNullOrEmpty(canhBao))
                        {
                            canhBao = $"{tenGv} vi phạm thời gian hồi phục sức khỏe (<10h) giữa ca tối Thứ {bt.Thu} (tiết {bt.TietKetThuc}) và ca sáng Thứ {bs.Thu} (tiết {bs.TietBatDau}).";
                        }
                    }
                }
            }
        }

        return diemPhat;
    }

    public double TinhDiemHoiPhucSucKhoe(int maGiangVien, List<int> dsMaLopPhanCong, BoiCanhDanhGia boiCanh)
    {
        return TinhDiemHoiPhucSucKhoe(maGiangVien, dsMaLopPhanCong, boiCanh, out _);
    }

    /// <summary>
    /// Nạp và đóng gói toàn bộ dữ liệu của học kỳ từ CSDL thành BoiCanhDanhGia để phục vụ xếp lịch.
    /// </summary>
    public async Task<BoiCanhDanhGia> TaoBoiCanhAsync(int maHocKy, CancellationToken cancellationToken = default)
    {
        if (_context == null) throw new InvalidOperationException("AppDbContext chua duoc khoi tao.");

        var dsLop = await _context.LopHocPhan.Where(x => x.MaHocKy == maHocKy).ToListAsync(cancellationToken);
        var dsMaLop = dsLop.Select(x => x.MaLopHocPhan).ToList();
        var dsChiTiet = await _context.ChiTietThoiKhoaBieu.Where(x => dsMaLop.Contains(x.MaLopHocPhan)).ToListAsync(cancellationToken);
        var dsGiangVien = await _context.GiangVien.Where(x => x.TrangThai != "NghiViec").ToListAsync(cancellationToken);
        var dsNangLuc = await _context.NangLucGiangDay.Where(x => string.IsNullOrEmpty(x.TrangThai) || x.TrangThai == "DangApDung").ToListAsync(cancellationToken);
        var dsLichBan = await _context.LichBanGiangVien.Where(x => x.MaHocKy == maHocKy).ToListAsync(cancellationToken);
        var dsNguyenVong = await _context.NguyenVongGiangVien.Where(x => x.MaHocKy == maHocKy).ToListAsync(cancellationToken);
        var dsDinhMuc = await _context.DinhMucGiangVien.Where(x => x.MaHocKy == maHocKy).ToListAsync(cancellationToken);
        var dsRangBuoc = await _context.RangBuocHocKy.Where(x => x.MaHocKy == maHocKy).ToListAsync(cancellationToken);

        var boiCanh = new BoiCanhDanhGia
        {
            MaHocKy = maHocKy,
            DanhSachLop = dsLop,
            LopTheoMa = dsLop.ToDictionary(x => x.MaLopHocPhan),
            ChiTietTheoLop = dsChiTiet.GroupBy(x => x.MaLopHocPhan).ToDictionary(g => g.Key, g => g.ToList()),
            GiangVienTheoMa = dsGiangVien.ToDictionary(x => x.MaGiangVien),
            NangLucTheoGv = dsNangLuc.GroupBy(x => x.MaGiangVien).ToDictionary(g => g.Key, g => g.Select(x => x.MaHocPhan).ToHashSet()),
            LichBanTheoGv = dsLichBan.GroupBy(x => x.MaGiangVien).ToDictionary(g => g.Key, g => g.ToList()),
            NguyenVongTheoGv = dsNguyenVong.GroupBy(x => x.MaGiangVien).ToDictionary(g => g.Key, g => g.ToList()),
            DinhMucTheoGv = dsDinhMuc.ToDictionary(x => x.MaGiangVien),
            TrongSoRangBuoc = dsRangBuoc.GroupBy(x => x.TenRangBuoc).ToDictionary(g => g.Key, g => g.First().TrongSoPhat)
        };

        return boiCanh;
    }

    // ==========================================
    // CÁC HÀM CŨ ĐỂ ĐẢM BẢO TƯƠNG THÍCH MÃ NGUỒN
    // ==========================================
    public double TinhDiemNguyenVong(int maGiangVien, int maLopHocPhan)
    {
        _ = (maGiangVien, maLopHocPhan);
        return 0;
    }

    public double TinhDiemGomLich(int maGiangVien)
    {
        _ = maGiangVien;
        return 0;
    }

    public double TinhDiemCanBangTai(int maGiangVien)
    {
        _ = maGiangVien;
        return 0;
    }

    public double TinhTongDiemPhat(IEnumerable<double> danhSachDiem)
    {
        return danhSachDiem.Sum();
    }
}
