using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;

namespace XepLichGiangDay.Api.Services;

public class XepLichService(
    AppDbContext context,
    BoKiemTraRangBuoc boKiemTra)
{
    /// <summary>
    /// Lấy bảng dữ liệu phân công giảng dạy tổng hợp cho toàn bộ học kỳ.
    /// </summary>
    public async Task<BangPhanCongHocKyDto> LayBangPhanCongAsync(int maHocKy, CancellationToken cancellationToken = default)
    {
        var hocKy = await context.HocKy.FirstOrDefaultAsync(x => x.MaHocKy == maHocKy, cancellationToken);
        var tenHocKy = hocKy != null ? hocKy.TenHocKy : $"Học kỳ {maHocKy}";

        // 1. Lấy danh sách lớp học phần trong kỳ
        var dsLop = await context.LopHocPhan
            .Where(x => x.MaHocKy == maHocKy)
            .OrderBy(x => x.MaLopHocPhanTruong)
            .ToListAsync(cancellationToken);

        var dsMaLop = dsLop.Select(x => x.MaLopHocPhan).ToList();
        var dsMaHp = dsLop.Select(x => x.MaHocPhan).Distinct().ToList();

        // 2. Lấy thông tin học phần và chi tiết thời khóa biểu
        var dsHocPhan = await context.HocPhan
            .Where(x => dsMaHp.Contains(x.MaHocPhan))
            .ToDictionaryAsync(x => x.MaHocPhan, cancellationToken);

        var dsChiTiet = await context.ChiTietThoiKhoaBieu
            .Where(x => dsMaLop.Contains(x.MaLopHocPhan))
            .ToListAsync(cancellationToken);
        var chiTietTheoLop = dsChiTiet.GroupBy(x => x.MaLopHocPhan).ToDictionary(g => g.Key, g => g.ToList());

        // 3. Lấy phân công hiện tại
        var dsPhanCong = await context.PhanCongGiangDay
            .Where(x => x.MaHocKy == maHocKy)
            .ToListAsync(cancellationToken);
        var phanCongTheoLop = dsPhanCong.GroupBy(x => x.MaLopHocPhan).ToDictionary(g => g.Key, g => g.First());

        // 4. Lấy giảng viên, năng lực giảng dạy, lịch bận
        var dsGiangVien = await context.GiangVien
            .Where(x => x.TrangThai != "NghiViec")
            .ToListAsync(cancellationToken);
        var dictGiangVien = dsGiangVien.ToDictionary(x => x.MaGiangVien);

        var dsNangLuc = await context.NangLucGiangDay.ToListAsync(cancellationToken);
        var dsLichBan = await context.LichBanGiangVien
            .Where(x => x.MaHocKy == maHocKy)
            .ToListAsync(cancellationToken);

        var nangLucTheoHp = dsNangLuc
            .Where(nl => string.IsNullOrEmpty(nl.TrangThai) || nl.TrangThai == "DangApDung")
            .GroupBy(x => x.MaHocPhan)
            .ToDictionary(g => g.Key, g => g.Select(x => x.MaGiangVien).ToHashSet());

        var lichBanTheoGv = dsLichBan
            .GroupBy(x => x.MaGiangVien)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Nhóm phân công hiện tại theo giảng viên để kiểm tra trùng giờ dạy giữa các lớp
        var phanCongHienTaiTheoGv = dsPhanCong
            .GroupBy(x => x.MaGiangVien)
            .ToDictionary(g => g.Key, g => g.Select(x => x.MaLopHocPhan).ToList());

        var danhSachDong = new List<DongPhanCongLopDto>();

        foreach (var lop in dsLop)
        {
            var hp = dsHocPhan.TryGetValue(lop.MaHocPhan, out var hocPhan) ? hocPhan : null;
            var cacBuoiHoc = chiTietTheoLop.TryGetValue(lop.MaLopHocPhan, out var dsCtLopHoc) ? dsCtLopHoc : [];
            var ct = cacBuoiHoc.FirstOrDefault();
            var pc = phanCongTheoLop.TryGetValue(lop.MaLopHocPhan, out var phanCong) ? phanCong : null;

            var dsBuoiHocDto = cacBuoiHoc.Select(b => new ChiTietBuoiHocDto
            {
                Thu = b.Thu,
                TietBatDau = b.TietBatDau,
                TietKetThuc = b.TietKetThuc,
                PhongHoc = b.PhongHoc ?? string.Empty,
                TuTuan = b.TuTuan,
                DenTuan = b.DenTuan
            }).ToList();

            string thoiGianTongHop = string.Join("; ", cacBuoiHoc.Select(b =>
                $"Thứ {b.Thu} (T{b.TietBatDau}-{b.TietKetThuc}){(string.IsNullOrWhiteSpace(b.PhongHoc) ? "" : $" P.{b.PhongHoc}")}"));

            var dong = new DongPhanCongLopDto
            {
                MaLopHocPhan = lop.MaLopHocPhan,
                MaLopHocPhanTruong = lop.MaLopHocPhanTruong,
                MaHocPhan = lop.MaHocPhan,
                TenHocPhan = hp != null ? hp.TenHocPhan : string.Empty,
                SoTinChi = hp != null ? hp.SoTinChi : 0,
                TenLop = lop.TenLop,
                SoLuongSinhVien = lop.SoLuongSinhVien,
                Thu = ct != null ? ct.Thu : 0,
                TietBatDau = ct != null ? ct.TietBatDau : 0,
                TietKetThuc = ct != null ? ct.TietKetThuc : 0,
                PhongHoc = ct != null ? ct.PhongHoc : string.Empty,
                TuTuan = ct != null ? ct.TuTuan : 0,
                DenTuan = ct != null ? ct.DenTuan : 0,
                ThoiGianHocTongHop = thoiGianTongHop,
                DanhSachBuoiHoc = dsBuoiHocDto
            };

            if (pc != null)
            {
                dong.MaPhanCong = pc.MaPhanCong;
                dong.MaGiangVien = pc.MaGiangVien;
                dong.NguonPhanCong = pc.NguonPhanCong;
                dong.TrangThai = pc.TrangThai;
                dong.DiemPhat = pc.DiemPhat;

                if (dictGiangVien.TryGetValue(pc.MaGiangVien, out var gv))
                {
                    dong.TenGiangVien = gv.HoTen;
                    dong.EmailGiangVien = gv.Email;
                }
            }

            // Gợi ý danh sách ứng viên cho lớp này
            var dsGvIdCoNangLuc = nangLucTheoHp.TryGetValue(lop.MaHocPhan, out var gvIds) ? gvIds : [];
            var dsUngVien = new List<UngVienLopDto>();

            foreach (var gv in dsGiangVien)
            {
                bool coNl = dsGvIdCoNangLuc.Contains(gv.MaGiangVien);
                if (!coNl) continue; // Chỉ gợi ý những GV có chuyên môn

                bool vuongLb = false;
                if (cacBuoiHoc.Count > 0 && lichBanTheoGv.TryGetValue(gv.MaGiangVien, out var dsLbCuaGv))
                {
                    vuongLb = cacBuoiHoc.Any(b => boKiemTra.CoVuongLichBan(gv.MaGiangVien, b.Thu, b.TietBatDau, b.TietKetThuc, dsLbCuaGv));
                }

                bool trungLd = false;
                if (cacBuoiHoc.Count > 0 && phanCongHienTaiTheoGv.TryGetValue(gv.MaGiangVien, out var cacMaLopCuaGv))
                {
                    foreach (var otherMaLop in cacMaLopCuaGv)
                    {
                        if (otherMaLop == lop.MaLopHocPhan) continue; // Bỏ qua chính lớp đang xét
                        if (chiTietTheoLop.TryGetValue(otherMaLop, out var otherCacBuoi) && otherCacBuoi != null)
                        {
                            foreach (var bCurrent in cacBuoiHoc)
                            {
                                foreach (var bOther in otherCacBuoi)
                                {
                                    if (boKiemTra.CoTrungLich(
                                        bCurrent.Thu, bCurrent.TietBatDau, bCurrent.TietKetThuc, bCurrent.TuTuan, bCurrent.DenTuan,
                                        bOther.Thu, bOther.TietBatDau, bOther.TietKetThuc, bOther.TuTuan, bOther.DenTuan))
                                    {
                                        trungLd = true;
                                        break;
                                    }
                                }
                                if (trungLd) break;
                            }
                        }
                        if (trungLd) break;
                    }
                }


                var lyDo = new List<string>();
                if (vuongLb) lyDo.Add("Vướng lịch bận cá nhân");
                if (trungLd) lyDo.Add("Trùng giờ dạy với lớp khác");

                dsUngVien.Add(new UngVienLopDto
                {
                    MaGiangVien = gv.MaGiangVien,
                    HoTen = gv.HoTen,
                    Email = gv.Email,
                    ChucDanh = gv.ChucDanh,
                    CoNangLuc = true,
                    BiVuongLichBan = vuongLb,
                    BiTrungLichDay = trungLd,
                    LyDoKhongHopLe = string.Join("; ", lyDo)
                });
            }

            // Sắp xếp ứng viên hợp lệ lên đầu
            dong.DanhSachUngVien = dsUngVien
                .OrderByDescending(x => x.HopLe)
                .ThenBy(x => x.HoTen)
                .ToList();

            danhSachDong.Add(dong);
        }

        return new BangPhanCongHocKyDto
        {
            MaHocKy = maHocKy,
            TenHocKy = tenHocKy,
            TongSoLop = danhSachDong.Count,
            SoLopDaPhanCong = danhSachDong.Count(x => x.MaGiangVien.HasValue && x.MaGiangVien > 0),
            SoLopChuaPhanCong = danhSachDong.Count(x => !x.MaGiangVien.HasValue || x.MaGiangVien <= 0),
            SoLopDaKhoa = danhSachDong.Count(x => x.DaKhoa),
            DanhSachLop = danhSachDong
        };
    }

    /// <summary>
    /// Can thiệp gán hoặc đổi giảng viên cho một lớp học phần bằng tay.
    /// Có kiểm tra ràng buộc tức thì (chuyên môn, lịch bận, trùng giờ).
    /// </summary>
    public async Task<KetQuaPhanCongThuCongDto> PhanCongThuCongAsync(YeuCauPhanCongThuCongDto req, CancellationToken cancellationToken = default)
    {
        var lop = await context.LopHocPhan.FirstOrDefaultAsync(x => x.MaLopHocPhan == req.MaLopHocPhan, cancellationToken);
        if (lop == null)
        {
            return new KetQuaPhanCongThuCongDto
            {
                ThanhCong = false,
                ThongBao = "Không tìm thấy lớp học phần."
            };
        }

        var gv = await context.GiangVien.FirstOrDefaultAsync(x => x.MaGiangVien == req.MaGiangVien, cancellationToken);
        if (gv == null)
        {
            return new KetQuaPhanCongThuCongDto
            {
                ThanhCong = false,
                ThongBao = "Không tìm thấy giảng viên."
            };
        }

        var viPham = new List<string>();

        // 1. Kiểm tra năng lực chuyên môn
        var coNangLuc = await context.NangLucGiangDay.AnyAsync(
            x => x.MaGiangVien == req.MaGiangVien && x.MaHocPhan == lop.MaHocPhan && (string.IsNullOrEmpty(x.TrangThai) || x.TrangThai == "DangApDung"),
            cancellationToken);

        if (!coNangLuc)
        {
            viPham.Add($"Giảng viên {gv.HoTen} không có năng lực giảng dạy học phần này.");
        }

        // 2. Lấy toàn bộ các buổi học của lớp (hỗ trợ đầy đủ các lớp có nhiều buổi/tuần)
        var dsCtLop = await context.ChiTietThoiKhoaBieu
            .Where(x => x.MaLopHocPhan == req.MaLopHocPhan)
            .ToListAsync(cancellationToken);

        if (dsCtLop.Count > 0)
        {
            // 2.1 Kiểm tra vướng lịch bận cá nhân cho tất cả các buổi
            var dsLb = await context.LichBanGiangVien
                .Where(x => x.MaGiangVien == req.MaGiangVien && x.MaHocKy == lop.MaHocKy)
                .ToListAsync(cancellationToken);

            foreach (var ctLop in dsCtLop)
            {
                if (boKiemTra.CoVuongLichBan(req.MaGiangVien, ctLop.Thu, ctLop.TietBatDau, ctLop.TietKetThuc, dsLb))
                {
                    viPham.Add($"Giảng viên {gv.HoTen} bị vướng lịch bận cá nhân vào Thứ {ctLop.Thu}, tiết {ctLop.TietBatDau}-{ctLop.TietKetThuc}.");
                }
            }

            // 2.2 Kiểm tra trùng giờ dạy với lớp khác trong cùng học kỳ
            var dsLopKhacCuaGv = await context.PhanCongGiangDay
                .Where(x => x.MaHocKy == lop.MaHocKy && x.MaGiangVien == req.MaGiangVien && x.MaLopHocPhan != req.MaLopHocPhan)
                .Select(x => x.MaLopHocPhan)
                .ToListAsync(cancellationToken);

            if (dsLopKhacCuaGv.Count > 0)
            {
                var dsChiTietKhac = await context.ChiTietThoiKhoaBieu
                    .Where(x => dsLopKhacCuaGv.Contains(x.MaLopHocPhan))
                    .ToListAsync(cancellationToken);

                var mapLopKhac = await context.LopHocPhan
                    .Where(x => dsLopKhacCuaGv.Contains(x.MaLopHocPhan))
                    .ToDictionaryAsync(x => x.MaLopHocPhan, cancellationToken);

                foreach (var ctLop in dsCtLop)
                {
                    foreach (var ctKhac in dsChiTietKhac)
                    {
                        if (boKiemTra.CoTrungLich(
                            ctLop.Thu, ctLop.TietBatDau, ctLop.TietKetThuc, ctLop.TuTuan, ctLop.DenTuan,
                            ctKhac.Thu, ctKhac.TietBatDau, ctKhac.TietKetThuc, ctKhac.TuTuan, ctKhac.DenTuan))
                        {
                            string maLopKhac = mapLopKhac.TryGetValue(ctKhac.MaLopHocPhan, out var lk) ? lk.MaLopHocPhanTruong : $"#{ctKhac.MaLopHocPhan}";
                            viPham.Add($"Trùng giờ dạy buổi Thứ {ctLop.Thu} (tiết {ctLop.TietBatDau}-{ctLop.TietKetThuc}) với lớp [{maLopKhac}] (Thứ {ctKhac.Thu}, tiết {ctKhac.TietBatDau}-{ctKhac.TietKetThuc}).");
                        }
                    }
                }
            }
        }


        // Nếu có vi phạm và người dùng chưa xác nhận bỏ qua cảnh báo
        if (viPham.Count > 0 && !req.BoQuaCanhBao)
        {
            return new KetQuaPhanCongThuCongDto
            {
                ThanhCong = false,
                ThongBao = "Phát hiện vi phạm ràng buộc phân công.",
                DanhSachViPham = viPham,
                CanXacNhan = true
            };
        }

        // Lưu / cập nhật bảng phan_cong_giang_day
        var phanCong = await context.PhanCongGiangDay
            .FirstOrDefaultAsync(x => x.MaLopHocPhan == req.MaLopHocPhan, cancellationToken);

        if (phanCong != null)
        {
            phanCong.MaGiangVien = req.MaGiangVien;
            phanCong.NguonPhanCong = "ThuCong";
            phanCong.TrangThai = req.KhoaNgay ? "DaKhoa" : "DuKien";
        }
        else
        {
            phanCong = new PhanCongGiangDay
            {
                MaLopHocPhan = req.MaLopHocPhan,
                MaGiangVien = req.MaGiangVien,
                MaHocKy = lop.MaHocKy,
                NguonPhanCong = "ThuCong",
                TrangThai = req.KhoaNgay ? "DaKhoa" : "DuKien",
                DiemPhat = 0
            };
            context.PhanCongGiangDay.Add(phanCong);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new KetQuaPhanCongThuCongDto
        {
            ThanhCong = true,
            ThongBao = $"Đã phân công thành công cho giảng viên {gv.HoTen}.",
            DanhSachViPham = viPham
        };
    }

    /// <summary>
    /// Khóa hoặc mở khóa trạng thái phân công của lớp.
    /// </summary>
    public async Task<bool> DoiTrangThaiKhoaAsync(int maLopHocPhan, bool daKhoa, CancellationToken cancellationToken = default)
    {
        var phanCong = await context.PhanCongGiangDay
            .FirstOrDefaultAsync(x => x.MaLopHocPhan == maLopHocPhan, cancellationToken);

        if (phanCong == null) return false;

        phanCong.TrangThai = daKhoa ? "DaKhoa" : "DuKien";
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Hủy phân công của một lớp học phần.
    /// </summary>
    public async Task<bool> HuyPhanCongAsync(int maLopHocPhan, CancellationToken cancellationToken = default)
    {
        var phanCong = await context.PhanCongGiangDay
            .FirstOrDefaultAsync(x => x.MaLopHocPhan == maLopHocPhan, cancellationToken);

        if (phanCong == null) return false;

        context.PhanCongGiangDay.Remove(phanCong);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Thống kê khối lượng tải giảng dạy của tất cả giảng viên trong học kỳ và so sánh với định mức.
    /// </summary>
    public async Task<List<ThongKeTaiGvDto>> LayThongKeTaiAsync(int maHocKy, CancellationToken cancellationToken = default)
    {
        var dsGiangVien = await context.GiangVien
            .Where(x => x.TrangThai != "NghiViec")
            .OrderBy(x => x.HoTen)
            .ToListAsync(cancellationToken);

        var dsPhanCong = await context.PhanCongGiangDay
            .Where(x => x.MaHocKy == maHocKy)
            .ToListAsync(cancellationToken);

        var dsMaLop = dsPhanCong.Select(x => x.MaLopHocPhan).Distinct().ToList();

        var dsLop = await context.LopHocPhan
            .Where(x => dsMaLop.Contains(x.MaLopHocPhan))
            .ToDictionaryAsync(x => x.MaLopHocPhan, cancellationToken);

        var dsMaHp = dsLop.Values.Select(x => x.MaHocPhan).Distinct().ToList();
        var dsHocPhan = await context.HocPhan
            .Where(x => dsMaHp.Contains(x.MaHocPhan))
            .ToDictionaryAsync(x => x.MaHocPhan, cancellationToken);

        var dsChiTiet = await context.ChiTietThoiKhoaBieu
            .Where(x => dsMaLop.Contains(x.MaLopHocPhan))
            .ToListAsync(cancellationToken);
        var chiTietTheoLop = dsChiTiet.GroupBy(x => x.MaLopHocPhan).ToDictionary(g => g.Key, g => g.FirstOrDefault());

        var dsDinhMuc = await context.DinhMucGiangVien
            .Where(x => x.MaHocKy == maHocKy && (string.IsNullOrEmpty(x.TrangThai) || x.TrangThai == "DangApDung"))
            .ToDictionaryAsync(x => x.MaGiangVien, cancellationToken);

        var phanCongTheoGv = dsPhanCong.GroupBy(x => x.MaGiangVien).ToDictionary(g => g.Key, g => g.ToList());

        var ketQua = new List<ThongKeTaiGvDto>();

        foreach (var gv in dsGiangVien)
        {
            var item = new ThongKeTaiGvDto
            {
                MaGiangVien = gv.MaGiangVien,
                HoTen = gv.HoTen,
                Email = gv.Email,
                ChucDanh = gv.ChucDanh
            };

            if (dsDinhMuc.TryGetValue(gv.MaGiangVien, out var dm))
            {
                item.SoTietToiThieu = dm.SoTietToiThieu;
                item.SoTietToiDa = dm.SoTietToiDa;
            }

            if (phanCongTheoGv.TryGetValue(gv.MaGiangVien, out var cacPhanCong))
            {
                item.SoLopPhanCong = cacPhanCong.Count;

                foreach (var pc in cacPhanCong)
                {
                    if (dsLop.TryGetValue(pc.MaLopHocPhan, out var lop) && dsHocPhan.TryGetValue(lop.MaHocPhan, out var hp))
                    {
                        item.TongSoTinChi += hp.SoTinChi;
                    }

                    if (chiTietTheoLop.TryGetValue(pc.MaLopHocPhan, out var ct) && ct != null)
                    {
                        int soTietLop = ct.TietKetThuc >= ct.TietBatDau ? (ct.TietKetThuc - ct.TietBatDau + 1) : 0;
                        item.TongSoTietTuan += soTietLop;
                    }
                }
            }

            // Đánh giá trạng thái tải so với định mức cả học kỳ (mặc định 60 - 120 tiết)
            int tongSoTietHocKy = item.TongSoTinChi > 0 ? (item.TongSoTinChi * 15) : (item.TongSoTietTuan * 15);

            if (item.SoTietToiDa > 0 && tongSoTietHocKy > item.SoTietToiDa)
            {
                item.TrangThaiTai = "QuaTai";
            }
            else if (item.SoTietToiThieu > 0 && tongSoTietHocKy < item.SoTietToiThieu)
            {
                item.TrangThaiTai = "ThieuTai";
            }
            else
            {
                item.TrangThaiTai = "BinhThuong";
            }

            ketQua.Add(item);
        }

        return ketQua;
    }

    /// <summary>
    /// Xuất toàn bộ bảng phân công thời khóa biểu và thống kê tải của học kỳ ra file Excel.
    /// </summary>
    public async Task<byte[]> XuatExcelBangPhanCongAsync(int maHocKy, CancellationToken cancellationToken = default)
    {
        var bangPhanCong = await LayBangPhanCongAsync(maHocKy, cancellationToken);
        var thongKeTai = await LayThongKeTaiAsync(maHocKy, cancellationToken);

        using var workbook = new XLWorkbook();

        // 1. Sheet 1: Thời khóa biểu & Phân công
        var ws1 = workbook.Worksheets.Add("ThoiKhoaBieu");

        // Tiêu đề
        ws1.Cell(1, 1).Value = $"BẢNG THỜI KHÓA BIỂU VÀ PHÂN CÔNG GIẢNG DẠY - {bangPhanCong.TenHocKy.ToUpperInvariant()}";
        ws1.Range(1, 1, 1, 15).Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // Header
        string[] headers1 = [
            "STT", "Mã lớp HP", "Tên học phần", "Số TC", "Lớp sinh viên", "SL SV",
            "Thứ", "Tiết bắt đầu", "Tiết kết thúc", "Số tiết", "Phòng", "Tuần học",
            "Giảng viên phân công", "Trạng thái", "Nguồn phân công"
        ];

        for (int i = 0; i < headers1.Length; i++)
        {
            var cell = ws1.Cell(3, i + 1);
            cell.Value = headers1[i];
            cell.Style.Font.SetBold(true);
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#EFEFEF"));
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int row1 = 4;
        int stt1 = 1;
        foreach (var lop in bangPhanCong.DanhSachLop)
        {
            ws1.Cell(row1, 1).Value = stt1++;
            ws1.Cell(row1, 2).Value = lop.MaLopHocPhanTruong;
            ws1.Cell(row1, 3).Value = lop.TenHocPhan;
            ws1.Cell(row1, 4).Value = lop.SoTinChi;
            ws1.Cell(row1, 5).Value = lop.TenLop;
            ws1.Cell(row1, 6).Value = lop.SoLuongSinhVien;
            ws1.Cell(row1, 7).Value = lop.Thu > 0 ? $"Thứ {lop.Thu}" : "-";
            ws1.Cell(row1, 8).Value = lop.TietBatDau > 0 ? lop.TietBatDau : "-";
            ws1.Cell(row1, 9).Value = lop.TietKetThuc > 0 ? lop.TietKetThuc : "-";
            ws1.Cell(row1, 10).Value = lop.SoTiet;
            ws1.Cell(row1, 11).Value = lop.PhongHoc;
            ws1.Cell(row1, 12).Value = lop.TuTuan > 0 ? $"{lop.TuTuan}-{lop.DenTuan}" : "-";
            ws1.Cell(row1, 13).Value = !string.IsNullOrEmpty(lop.TenGiangVien) ? lop.TenGiangVien : "Chưa phân công";
            ws1.Cell(row1, 14).Value = lop.DaKhoa ? "Đã khóa" : (!string.IsNullOrEmpty(lop.TenGiangVien) ? "Dự kiến" : "Chưa phân công");
            ws1.Cell(row1, 15).Value = lop.NguonPhanCong == "ThuCong" ? "Thủ công" : lop.NguonPhanCong;

            // Kẻ viền và căn chỉnh
            for (int col = 1; col <= 15; col++)
            {
                ws1.Cell(row1, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            ws1.Cell(row1, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 10).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 11).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 12).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 14).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws1.Cell(row1, 15).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            row1++;
        }
        ws1.Columns().AdjustToContents();

        // 2. Sheet 2: Thống kê định mức tải giảng viên
        var ws2 = workbook.Worksheets.Add("ThongKeTaiGiangVien");

        ws2.Cell(1, 1).Value = $"BẢNG TỔNG HỢP TẢI GIẢNG DẠY GIẢNG VIÊN - {bangPhanCong.TenHocKy.ToUpperInvariant()}";
        ws2.Range(1, 1, 1, 9).Merge().Style.Font.SetBold(true).Font.SetFontSize(14).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        string[] headers2 = [
            "STT", "Họ và tên", "Email", "Chức danh", "Số lớp đảm nhiệm",
            "Tổng số tín chỉ", "Tổng số tiết/tuần", "Định mức (Min - Max)", "Trạng thái tải"
        ];

        for (int i = 0; i < headers2.Length; i++)
        {
            var cell = ws2.Cell(3, i + 1);
            cell.Value = headers2[i];
            cell.Style.Font.SetBold(true);
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#EFEFEF"));
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int row2 = 4;
        int stt2 = 1;
        foreach (var gv in thongKeTai)
        {
            string dinhMucStr = gv.SoTietToiThieu > 0 || gv.SoTietToiDa > 0 ? $"{gv.SoTietToiThieu} - {gv.SoTietToiDa} tiết" : "Chưa cấu hình";
            string trangThaiStr = gv.TrangThaiTai switch
            {
                "ThieuTai" => "Thiếu tải",
                "QuaTai" => "Quá tải",
                _ => "Đạt định mức"
            };

            ws2.Cell(row2, 1).Value = stt2++;
            ws2.Cell(row2, 2).Value = gv.HoTen;
            ws2.Cell(row2, 3).Value = gv.Email;
            ws2.Cell(row2, 4).Value = gv.ChucDanh ?? "-";
            ws2.Cell(row2, 5).Value = gv.SoLopPhanCong;
            ws2.Cell(row2, 6).Value = gv.TongSoTinChi;
            ws2.Cell(row2, 7).Value = gv.TongSoTietTuan;
            ws2.Cell(row2, 8).Value = dinhMucStr;
            ws2.Cell(row2, 9).Value = trangThaiStr;

            for (int col = 1; col <= 9; col++)
            {
                ws2.Cell(row2, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            ws2.Cell(row2, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws2.Cell(row2, 4).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws2.Cell(row2, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws2.Cell(row2, 6).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws2.Cell(row2, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws2.Cell(row2, 8).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws2.Cell(row2, 9).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            row2++;
        }
        ws2.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
