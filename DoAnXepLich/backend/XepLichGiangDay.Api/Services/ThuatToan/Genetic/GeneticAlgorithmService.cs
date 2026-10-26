using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;

namespace XepLichGiangDay.Api.Services.ThuatToan.Genetic;

public class GeneticAlgorithmService(
    AppDbContext context,
    BoDanhGiaPhuongAn boDanhGia,
    TaoDanhSachUngVien taoUngVien,
    LuuKetQuaXepLichService luuKetQuaService) : ISchedulingAlgorithm
{
    public string TenThuatToan => "GA";

    public async Task<KetQuaXepLichDto> ChayAsync(DuLieuXepLichDto duLieu, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Nạp ngữ cảnh dữ liệu học kỳ vào bộ nhớ đệm
        var boiCanh = await boDanhGia.TaoBoiCanhAsync(duLieu.MaHocKy, cancellationToken);
        if (boiCanh.DanhSachLop.Count == 0)
        {
            return new KetQuaXepLichDto
            {
                ThuatToan = TenThuatToan,
                TrangThai = "KhongCoLopHocPhan",
                ThongDiep = "Học kỳ này chưa có lớp học phần nào để xếp lịch.",
                TongDiemPhat = 0
            };
        }

        // 2. Lấy danh sách lớp đã khóa ghim (H6/H8 - Integrity of locked assignments)
        var dsPhanCongHienTai = await context.PhanCongGiangDay
            .Where(x => x.MaHocKy == duLieu.MaHocKy)
            .ToListAsync(cancellationToken);

        var phanCongKhoa = dsPhanCongHienTai
            .Where(x => x.TrangThai == "DaKhoa")
            .GroupBy(x => x.MaLopHocPhan)
            .ToDictionary(g => g.Key, g => g.First().MaGiangVien);
        var cacLopCoDinh = phanCongKhoa.Keys.ToHashSet();

        // 3. Tạo danh sách ứng viên chuyên môn cho từng lớp học phần
        var dsNangLuc = await context.NangLucGiangDay
            .Where(x => string.IsNullOrEmpty(x.TrangThai) || x.TrangThai == "DangApDung")
            .ToListAsync(cancellationToken);

        var ungVienTheoLop = taoUngVien.TaoToanBo(
            boiCanh.DanhSachLop,
            boiCanh.ChiTietTheoLop.Values.SelectMany(x => x).ToList(),
            dsNangLuc,
            boiCanh.LichBanTheoGv.Values.SelectMany(x => x).ToList(),
            boiCanh.GiangVienTheoMa.Values.ToList());

        // 4. TIỀN KIỂM TRA TẬP ỨNG VIÊN (Domain Feasibility Check)
        // Nếu có bất kỳ lớp nào chưa khóa mà không có ứng viên hợp lệ -> dừng ngay và báo lỗi rõ ràng
        var cacLopKhongCoUngVien = new List<string>();
        foreach (var lop in boiCanh.DanhSachLop)
        {
            if (cacLopCoDinh.Contains(lop.MaLopHocPhan)) continue;

            if (!ungVienTheoLop.TryGetValue(lop.MaLopHocPhan, out var dsUv) || dsUv.Count == 0)
            {
                cacLopKhongCoUngVien.Add($"Lớp [{lop.MaLopHocPhanTruong}] (Mã HP: {lop.MaHocPhan}) không có bất kỳ giảng viên nào đủ năng lực chuyên môn.");
            }
        }

        if (cacLopKhongCoUngVien.Count > 0)
        {
            sw.Stop();
            return new KetQuaXepLichDto
            {
                ThuatToan = TenThuatToan,
                TrangThai = "KhongCoUngVienHopLe",
                TongDiemPhat = 999999,
                SoViPhamCung = cacLopKhongCoUngVien.Count,
                ThoiGianChayMiligiay = sw.ElapsedMilliseconds,
                ThoiGianChayGiay = Math.Round(sw.Elapsed.TotalSeconds, 2),
                ThongDiep = $"Phát hiện {cacLopKhongCoUngVien.Count} lớp học phần không có giảng viên nào đủ điều kiện chuyên môn. Không thể tìm được nghiệm hợp lệ.",
                DanhSachViPham = cacLopKhongCoUngVien
            };
        }

        // 5. Chuẩn bị bảng thời gian các lớp và lịch bận giảng viên phục vụ toán tử sửa lỗi & Heuristic Seeding
        var thoiGianTheoLop = boiCanh.ChiTietTheoLop.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(ct => new ThoiGianLop
            {
                MaLopHocPhan = ct.MaLopHocPhan,
                Thu = ct.Thu,
                TietBatDau = ct.TietBatDau,
                TietKetThuc = ct.TietKetThuc,
                TuTuan = ct.TuTuan > 0 ? ct.TuTuan : 1,
                DenTuan = ct.DenTuan > 0 ? ct.DenTuan : 53
            }).ToList()
        );

        var lichBanTheoGv = boiCanh.LichBanTheoGv.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.Select(lb => new ThoiGianBan
            {
                Thu = lb.Thu,
                TietBatDau = lb.TietBatDau,
                TietKetThuc = lb.TietKetThuc
            }).ToList()
        );

        // 6. Cấu hình tham số tiến hóa GA chuẩn hóa
        var cauHinh = new CauHinhGa
        {
            KichThuocQuanThe = duLieu.KichThuocQuanThe.GetValueOrDefault(50),
            SoTheHe = duLieu.SoTheHe.GetValueOrDefault(100),
            TyLeLaiGhep = duLieu.TyLeLaiGhep.GetValueOrDefault(0.8),
            TyLeDotBien = duLieu.TyLeDotBien.GetValueOrDefault(0.05),
            KichThuocGiaiDau = duLieu.KichThuocGiaiDau.GetValueOrDefault(3),
            SoCaTheUuTu = duLieu.SoCaTheUuTu.GetValueOrDefault(2),
            RandomSeed = duLieu.RandomSeed,
            ThoiGianChayToiDaGiay = duLieu.ThoiGianChayToiDa.GetValueOrDefault(30.0)
        };
        cauHinh.ChuanHoaVaXacThuc();

        // Khởi tạo Random với seed (nếu có) để đảm bảo tính tái lập 100%
        var random = cauHinh.RandomSeed.HasValue ? new Random(cauHinh.RandomSeed.Value) : new Random();

        var khoiTao = new KhoiTaoQuanThe(random);
        var chonLoc = new ChonLoc(random);
        var laiGhep = new LaiGhep(random);
        var dotBien = new DotBien(random);
        var suaCaThe = new SuaCaThe(random);

        var danhSachMaLop = boiCanh.DanhSachLop.Select(x => x.MaLopHocPhan).ToList();

        // 7. Khởi tạo quần thể ban đầu kết hợp Heuristic Seeding & Random
        var quanThe = khoiTao.TaoQuanTheBanDau(
            cauHinh.KichThuocQuanThe,
            danhSachMaLop,
            ungVienTheoLop,
            phanCongKhoa,
            thoiGianTheoLop,
            lichBanTheoGv);

        void DanhGiaQuanThe(List<CaThe> danhSach)
        {
            foreach (var ct in danhSach)
            {
                var dictPhanCong = ct.DanhSachGen.ToDictionary(g => g.MaLopHocPhan, g => (int?)g.MaGiangVien);
                var kq = boDanhGia.DanhGia(dictPhanCong, boiCanh);
                ct.KetQuaDanhGia = kq;
                ct.DiemPhat = kq.TongDiemPhat;
                ct.SoViPhamCung = kq.SoViPhamCung;
                ct.TongDiemPhatMem = kq.TongDiemPhatMem;
                ct.DoThichNghi = kq.DoThichNghi;
            }
        }

        // Đánh giá quần thể thế hệ 0
        DanhGiaQuanThe(quanThe);
        quanThe.Sort((a, b) => a.SoSanhVoi(b));

        var bestOverall = SaoChepCaThe(quanThe[0]);
        var lichSuDiemPhat = new List<double> { bestOverall.DiemPhat };

        int theHeThucTe = 0;
        int soTheHeKhongCaiThien = 0;
        const int NguongTriTre = 25;

        // 8. Vòng lặp tiến hóa qua các thế hệ
        for (int gen = 1; gen <= cauHinh.SoTheHe; gen++)
        {
            theHeThucTe = gen;
            cancellationToken.ThrowIfCancellationRequested();

            // Kiểm tra giới hạn thời gian (Time Limit)
            if (sw.Elapsed.TotalSeconds >= cauHinh.ThoiGianChayToiDaGiay)
            {
                break;
            }

            // Dừng sớm nếu đã tìm thấy phương án tối ưu tuyệt đối (hợp lệ và không còn điểm phạt mềm)
            if (bestOverall.LaHopLe && bestOverall.TongDiemPhatMem <= 0.0001)
            {
                break;
            }

            var theHeMoi = new List<CaThe>(cauHinh.KichThuocQuanThe);

            // Elitism: Bảo toàn k cá thể ưu tú nhất
            int eliteCount = Math.Min(cauHinh.SoCaTheUuTu, quanThe.Count);
            for (int e = 0; e < eliteCount; e++)
            {
                theHeMoi.Add(SaoChepCaThe(quanThe[e]));
            }

            // Cơ chế Đột biến thích nghi (Adaptive Mutation): tăng nhẹ tỷ lệ đột biến khi bị kẹt ở nghiệm vi phạm cứng
            double tyLeDotBienHienTai = cauHinh.TyLeDotBien;
            if (soTheHeKhongCaiThien >= 8 && !bestOverall.LaHopLe)
            {
                tyLeDotBienHienTai = Math.Min(0.20, cauHinh.TyLeDotBien * 2.0);
            }

            // Sinh các cá thể mới bằng: Selection -> Crossover -> Mutation -> Repair
            while (theHeMoi.Count < cauHinh.KichThuocQuanThe)
            {
                var cha = chonLoc.ChonCaTheChaMe(quanThe, cauHinh.KichThuocGiaiDau);
                var me = chonLoc.ChonCaTheChaMe(quanThe, cauHinh.KichThuocGiaiDau);

                var con = laiGhep.TaoCon(cha, me, cauHinh.TyLeLaiGhep, cacLopCoDinh);
                con = dotBien.ThucHien(con, tyLeDotBienHienTai, ungVienTheoLop, cacLopCoDinh);
                con = suaCaThe.SuaNeuViPham(con, thoiGianTheoLop, ungVienTheoLop, cacLopCoDinh, lichBanTheoGv);

                theHeMoi.Add(con);
            }

            // Đánh giá thế hệ mới và sắp xếp theo thứ tự phân tầng (Hard > Soft)
            DanhGiaQuanThe(theHeMoi);
            theHeMoi.Sort((a, b) => a.SoSanhVoi(b));
            quanThe = theHeMoi;

            // Cập nhật cá thể tốt nhất toàn cục
            if (quanThe[0].SoSanhVoi(bestOverall) < 0)
            {
                bestOverall = SaoChepCaThe(quanThe[0]);
                soTheHeKhongCaiThien = 0;
            }
            else
            {
                soTheHeKhongCaiThien++;
            }

            lichSuDiemPhat.Add(bestOverall.DiemPhat);

            // Dừng sớm khi quần thể đã hội tụ (nghiệm hợp lệ và không cải thiện sau N thế hệ liên tiếp)
            if (bestOverall.LaHopLe && soTheHeKhongCaiThien >= NguongTriTre)
            {
                break;
            }
        }

        sw.Stop();

        // 9. Kiểm tra độc lập nghiệm tốt nhất trước khi kết luận
        var dictNghiemCuoi = bestOverall.DanhSachGen.ToDictionary(g => g.MaLopHocPhan, g => (int?)g.MaGiangVien);
        var finalCheck = boDanhGia.DanhGia(dictNghiemCuoi, boiCanh);
        bestOverall.KetQuaDanhGia = finalCheck;
        bestOverall.SoViPhamCung = finalCheck.SoViPhamCung;
        bestOverall.TongDiemPhatMem = finalCheck.TongDiemPhatMem;

        var dsPhanCongDto = new List<PhanCongDto>();
        foreach (var gen in bestOverall.DanhSachGen)
        {
            if (gen.MaGiangVien.HasValue && gen.MaGiangVien.Value > 0)
            {
                double diemPhatLop = boDanhGia.TinhDiemNguyenVong(gen.MaGiangVien.Value, gen.MaLopHocPhan, boiCanh);
                dsPhanCongDto.Add(new PhanCongDto
                {
                    MaLopHocPhan = gen.MaLopHocPhan,
                    MaGiangVien = gen.MaGiangVien.Value,
                    DiemPhat = Math.Round(diemPhatLop, 1)
                });
            }
        }

        bool isRelational = context.Database.IsRelational();

        // 10. XỬ LÝ LƯU CSDL VỚI CƠ CHẾ BẢO VỆ DỮ LIỆU (Database Safety Guard)
        // Nếu còn vi phạm ràng buộc cứng (SoViPhamCung > 0): TUYỆT ĐỐI KHÔNG GHI ĐÈ bảng phan_cong_giang_day
        if (finalCheck.SoViPhamCung > 0)
        {
            // Chỉ ghi nhận nhật ký thất bại vào ket_qua_xep_lich mà không phá hỏng phân công đang có
            var transLog = isRelational ? await context.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var oldKq = await context.KetQuaXepLich.Where(x => x.MaHocKy == duLieu.MaHocKy).ToListAsync(cancellationToken);
                context.KetQuaXepLich.RemoveRange(oldKq);

                foreach (var gen in bestOverall.DanhSachGen)
                {
                    if (gen.MaGiangVien.HasValue && gen.MaGiangVien.Value > 0)
                    {
                        context.KetQuaXepLich.Add(new KetQuaXepLich
                        {
                            MaHocKy = duLieu.MaHocKy,
                            MaLopHocPhan = gen.MaLopHocPhan,
                            MaGiangVien = gen.MaGiangVien.Value,
                            DiemPhat = finalCheck.TongDiemPhat,
                            GhiChu = $"GA thế hệ {theHeThucTe} (Chưa thỏa mãn {finalCheck.SoViPhamCung} ràng buộc cứng)"
                        });
                    }
                }
                await context.SaveChangesAsync(cancellationToken);
                if (transLog != null) await transLog.CommitAsync(cancellationToken);
            }
            catch
            {
                if (transLog != null) await transLog.RollbackAsync(cancellationToken);
            }
            finally
            {
                transLog?.Dispose();
            }

            return new KetQuaXepLichDto
            {
                ThuatToan = TenThuatToan,
                TongDiemPhat = finalCheck.TongDiemPhat,
                DoThichNghi = finalCheck.DoThichNghi,
                DiemPhatRangBuocCung = finalCheck.DiemPhatRangBuocCung,
                DiemPhatNguyenVong = finalCheck.DiemPhatNguyenVong,
                DiemPhatCanBangTai = finalCheck.DiemPhatCanBangTai,
                DiemPhatGomLich = finalCheck.DiemPhatGomLich,
                SoViPhamCung = finalCheck.SoViPhamCung,
                SoLopChuaPhanCong = finalCheck.SoLopChuaPhanCong,
                SoTheHeThucTe = theHeThucTe,
                ThoiGianChayMiligiay = sw.ElapsedMilliseconds,
                ThoiGianChayGiay = Math.Round(sw.Elapsed.TotalSeconds, 2),
                TrangThai = "ConViPham",
                ThongDiep = $"Thuật toán GA đã chạy qua {theHeThucTe} thế hệ nhưng chưa tìm thấy phương án thỏa mãn 100% ràng buộc cứng (còn {finalCheck.SoViPhamCung} vi phạm). Hệ thống không ghi đè phân công hiện tại để bảo toàn tính toàn vẹn dữ liệu.",
                LichSuDiemPhatTheoTheHe = lichSuDiemPhat,
                DanhSachViPham = finalCheck.DanhSachViPham,
                ChiTietViPham = finalCheck.DanhSachCanhBaoMem,
                DanhSachPhanCong = dsPhanCongDto
            };
        }

        // KHI NGHIỆM THỎA MÃN 100% RÀNG BUỘC CỨNG: Cập nhật CSDL an toàn qua LuuKetQuaXepLichService
        var dictPhanCongCuoi = bestOverall.DanhSachGen.ToDictionary(g => g.MaLopHocPhan, g => (int?)g.MaGiangVien);
        var ketQuaLuu = await luuKetQuaService.LuuPhanCongAsync(
            duLieu.MaHocKy,
            TenThuatToan,
            dictPhanCongCuoi,
            boiCanh,
            cancellationToken);

        string thongBaoKhoa = ketQuaLuu.SoLopGiuKhoa > 0
            ? $" ({ketQuaLuu.SoLopGiuKhoa} lớp đã khóa được giữ nguyên phân công)."
            : "";

        string trangThaiKetQua = finalCheck.TongDiemPhatMem <= 0.0001
            ? "ToiUuTuyetDoi"
            : "ThoaManRangBuocCung";

            return new KetQuaXepLichDto
            {
                ThuatToan = TenThuatToan,
                TongDiemPhat = finalCheck.TongDiemPhat,
                DoThichNghi = finalCheck.DoThichNghi,
                DiemPhatRangBuocCung = finalCheck.DiemPhatRangBuocCung,
                DiemPhatNguyenVong = finalCheck.DiemPhatNguyenVong,
                DiemPhatCanBangTai = finalCheck.DiemPhatCanBangTai,
                DiemPhatGomLich = finalCheck.DiemPhatGomLich,
                SoViPhamCung = 0,
                SoLopChuaPhanCong = finalCheck.SoLopChuaPhanCong,
                SoTheHeThucTe = theHeThucTe,
                ThoiGianChayMiligiay = sw.ElapsedMilliseconds,
                ThoiGianChayGiay = Math.Round(sw.Elapsed.TotalSeconds, 2),
                TrangThai = trangThaiKetQua,
                ThongDiep = (trangThaiKetQua == "ToiUuTuyetDoi"
                    ? $"GA tìm thấy phương án tối ưu tuyệt đối (điểm phạt = 0) tại thế hệ {theHeThucTe}."
                    : $"GA tìm thấy phương án thỏa mãn 100% ràng buộc cứng sau {theHeThucTe} thế hệ (Tổng điểm phạt mềm: {Math.Round(finalCheck.TongDiemPhatMem, 1)}).") + thongBaoKhoa,
                LichSuDiemPhatTheoTheHe = lichSuDiemPhat,
                DanhSachViPham = finalCheck.DanhSachViPham,
                ChiTietViPham = finalCheck.DanhSachCanhBaoMem,
                DanhSachPhanCong = dsPhanCongDto
            };
    }

    private static CaThe SaoChepCaThe(CaThe goc)
    {
        return new CaThe
        {
            DiemPhat = goc.DiemPhat,
            SoViPhamCung = goc.SoViPhamCung,
            TongDiemPhatMem = goc.TongDiemPhatMem,
            DoThichNghi = goc.DoThichNghi,
            KetQuaDanhGia = goc.KetQuaDanhGia,
            DanhSachGen = goc.DanhSachGen.Select(g => new Gen
            {
                MaLopHocPhan = g.MaLopHocPhan,
                MaGiangVien = g.MaGiangVien
            }).ToList()
        };
    }
}
