using System.Diagnostics;
using Google.OrTools.Sat;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;

namespace XepLichGiangDay.Api.Services.ThuatToan.CpSat;

public class CpSatAlgorithmService(
    AppDbContext context,
    BoDanhGiaPhuongAn boDanhGia,
    BoKiemTraRangBuoc boKiemTra,
    LuuKetQuaXepLichService luuKetQuaService) : ISchedulingAlgorithm
{
    public string TenThuatToan => "CP_SAT";

    public async Task<KetQuaXepLichDto> ChayAsync(DuLieuXepLichDto duLieu, CancellationToken cancellationToken = default)
    {
        // 1. Nạp ngữ cảnh dữ liệu học kỳ
        var boiCanh = await boDanhGia.TaoBoiCanhAsync(duLieu.MaHocKy, cancellationToken);
        if (boiCanh.DanhSachLop.Count == 0)
        {
            return new KetQuaXepLichDto
            {
                ThuatToan = TenThuatToan,
                TongDiemPhat = 0,
                TrangThai = "KhongCoLop",
                ThongDiep = "Học kỳ này chưa có lớp học phần nào để xếp lịch."
            };
        }

        // Lấy danh sách lớp đã khóa phân công (H6 - Assignment Lock Integrity)
        var dsLopKhoa = await context.PhanCongGiangDay
            .Where(x => x.MaHocKy == duLieu.MaHocKy && x.TrangThai == "DaKhoa")
            .ToDictionaryAsync(x => x.MaLopHocPhan, x => x.MaGiangVien, cancellationToken);

        // 2. Khởi tạo mô hình toán học CP-SAT
        var model = new CpModel();
        var xVars = new Dictionary<(int MaGiangVien, int MaLopHocPhan), BoolVar>();
        var ungVienTheoLop = new Dictionary<int, List<int>>();
        var lopTheoGv = new Dictionary<int, List<int>>();

        foreach (var gv in boiCanh.GiangVienTheoMa.Values)
        {
            lopTheoGv[gv.MaGiangVien] = [];
        }

        // 3. Khởi tạo biến nhị phân với kỹ thuật DOMAIN PRUNING (cắt tỉa miền trước khi sinh biến)
        foreach (var lop in boiCanh.DanhSachLop)
        {
            int maLop = lop.MaLopHocPhan;
            ungVienTheoLop[maLop] = [];

            // Nếu lớp đã bị khóa phân công cố định
            if (dsLopKhoa.TryGetValue(maLop, out var maGvKhoa))
            {
                var xVar = model.NewBoolVar($"x_g{maGvKhoa}_l{maLop}");
                xVars[(maGvKhoa, maLop)] = xVar;
                ungVienTheoLop[maLop].Add(maGvKhoa);
                if (!lopTheoGv.ContainsKey(maGvKhoa)) lopTheoGv[maGvKhoa] = [];
                lopTheoGv[maGvKhoa].Add(maLop);

                // Ràng buộc cứng H6: Đã khóa thì bắt buộc bằng 1
                model.Add(xVar == 1);
                continue;
            }

            // Với các lớp chưa khóa: lọc ứng viên đủ năng lực (H2) và không vướng lịch bận cố định (H3)
            boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsBuoiHoc);
            dsBuoiHoc ??= [];

            foreach (var gv in boiCanh.GiangVienTheoMa.Values)
            {
                int maGv = gv.MaGiangVien;

                // H2: Đúng chuyên môn
                bool coChuyenMon = boiCanh.NangLucTheoGv.TryGetValue(maGv, out var setHp) && setHp.Contains(lop.MaHocPhan);
                if (!coChuyenMon) continue;

                // H3: Không vướng lịch bận cố định
                var dsLb = boiCanh.LichBanTheoGv.GetValueOrDefault(maGv) ?? [];
                bool vuongLichBan = dsBuoiHoc.Any(ct => boKiemTra.CoVuongLichBan(maGv, ct.Thu, ct.TietBatDau, ct.TietKetThuc, dsLb));
                if (vuongLichBan) continue;

                // Sinh biến nhị phân x[i, j]
                var xVar = model.NewBoolVar($"x_g{maGv}_l{maLop}");
                xVars[(maGv, maLop)] = xVar;
                ungVienTheoLop[maLop].Add(maGv);
                lopTheoGv[maGv].Add(maLop);
            }

            // Nếu một lớp không có bất kỳ giảng viên hợp lệ nào => Bài toán vô nghiệm ngay từ miền giá trị
            if (ungVienTheoLop[maLop].Count == 0)
            {
                return new KetQuaXepLichDto
                {
                    ThuatToan = TenThuatToan,
                    TongDiemPhat = 0,
                    TrangThai = "VoNghiem",
                    ThongDiep = $"Lớp [{lop.MaLopHocPhanTruong} - {lop.TenLop}] không có giảng viên nào đủ chuyên môn và rảnh lịch bận cố định."
                };
            }
        }

        // 4. Thiết lập HỆ THỐNG 6 RÀNG BUỘC CỨNG (HARD CONSTRAINTS)
        // (H5) Phủ kín 100% lớp học phần: Mỗi lớp phải do chính xác 1 giảng viên phụ trách
        foreach (var lop in boiCanh.DanhSachLop)
        {
            var dsBienLop = ungVienTheoLop[lop.MaLopHocPhan].Select(g => xVars[(g, lop.MaLopHocPhan)]).ToList();
            model.Add(LinearExpr.Sum(dsBienLop) == 1);
        }

        // (H1) Khống chế trùng lịch giảng viên: Một GV không dạy 2 lớp trùng thời gian
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count <= 1) continue;

            for (int i = 0; i < dsLopCuaGv.Count; i++)
            {
                for (int j = i + 1; j < dsLopCuaGv.Count; j++)
                {
                    int lop1 = dsLopCuaGv[i];
                    int lop2 = dsLopCuaGv[j];

                    var ct1 = boiCanh.ChiTietTheoLop.GetValueOrDefault(lop1) ?? [];
                    var ct2 = boiCanh.ChiTietTheoLop.GetValueOrDefault(lop2) ?? [];

                    bool trungLich = ct1.Any(b1 => ct2.Any(b2 =>
                        boKiemTra.CoTrungLich(b1.Thu, b1.TietBatDau, b1.TietKetThuc, b1.TuTuan, b1.DenTuan,
                                             b2.Thu, b2.TietBatDau, b2.TietKetThuc, b2.TuTuan, b2.DenTuan)));

                    if (trungLich)
                    {
                        model.Add(xVars[(maGv, lop1)] + xVars[(maGv, lop2)] <= 1);
                    }
                }
            }
        }

        // (H4) Định mức tiết dạy tối đa (Maximum Teaching Quota)
        // Thiết lập ngưỡng trần an toàn tuyệt đối và biến phạt vượt giờ (Soft Penalty)
        // Giúp bộ giải không bị vô nghiệm khi khoa thiếu giảng viên cơ hữu (tổng tiết học kỳ vượt quota lý thuyết)
        var dsMaxQuotaTheoGv = new Dictionary<int, int>();
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count == 0) continue;

            bool isThinhGiang = boiCanh.GiangVienTheoMa.TryGetValue(maGv, out var gv) &&
                ((gv.ChucDanh != null && gv.ChucDanh.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase)) ||
                 gv.HoTen.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase));

            int maxQuota = isThinhGiang ? 16 : 25;
            if (boiCanh.DinhMucTheoGv.TryGetValue(maGv, out var dm) && dm.SoTietToiDa > 0)
            {
                maxQuota = dm.SoTietToiDa;
            }
            dsMaxQuotaTheoGv[maGv] = maxQuota;

            var dsBienGv = new List<BoolVar>();
            var dsSoTiet = new List<long>();
            foreach (var maLop in dsLopCuaGv)
            {
                dsBienGv.Add(xVars[(maGv, maLop)]);
                dsSoTiet.Add(TinhSoTiet(maLop, boiCanh));
            }

            // Ngưỡng trần cứng tuyệt đối bảo vệ sức khỏe giảng viên (tối đa 40 tiết/tuần)
            model.Add(LinearExpr.WeightedSum(dsBienGv, dsSoTiet) <= Math.Max(maxQuota + 16, 40));
        }

        // 5. THIẾT LẬP HÀM MỤC TIÊU TỐI ƯU HÓA RÀNG BUỘC MỀM (SOFT CONSTRAINTS)
        // Tối thiểu hóa tổng chi phí phạt: Z_penalty
        var objectiveTerms = new List<LinearExpr>();

        // w1 * P1: Phạt vi phạm nguyện vọng ca dạy ưu tiên & ưu tiên GV cơ hữu
        foreach (var ((maGv, maLop), xVar) in xVars)
        {
            double diemPhatNv = boDanhGia.TinhDiemNguyenVong(maGv, maLop, boiCanh);
            if (diemPhatNv > 0)
            {
                long trongSoNguyen = (long)Math.Round(diemPhatNv * 10);
                objectiveTerms.Add(xVar * trongSoNguyen);
            }
        }

        // w2 * P2: Phạt gom lịch & hạn chế số ngày đi dạy trong tuần
        long trongSoGomLich = (long)Math.Round(boiCanh.TrongSoGomLich * 10);
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count == 0) continue;

            for (int thu = 2; thu <= 7; thu++)
            {
                var dsBienTrongNgay = new List<BoolVar>();
                foreach (var maLop in dsLopCuaGv)
                {
                    var dsBuoi = boiCanh.ChiTietTheoLop.GetValueOrDefault(maLop) ?? [];
                    if (dsBuoi.Any(b => b.Thu == thu))
                    {
                        dsBienTrongNgay.Add(xVars[(maGv, maLop)]);
                    }
                }

                if (dsBienTrongNgay.Count > 0)
                {
                    // dayUsed = 1 nếu GV có ít nhất 1 lớp dạy vào ngày thu
                    var dayUsed = model.NewBoolVar($"day_{maGv}_{thu}");
                    model.AddMaxEquality(dayUsed, dsBienTrongNgay);
                    objectiveTerms.Add(dayUsed * trongSoGomLich);
                }
            }
        }

        // w3 * P3: Phạt vi phạm thời gian hồi phục sức khỏe (< 10 tiếng giữa ca tối hôm trước và ca sáng sớm hôm sau)
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count <= 1) continue;

            for (int i = 0; i < dsLopCuaGv.Count; i++)
            {
                for (int j = 0; j < dsLopCuaGv.Count; j++)
                {
                    if (i == j) continue;
                    int lopToi = dsLopCuaGv[i];
                    int lopSang = dsLopCuaGv[j];

                    var ctToi = boiCanh.ChiTietTheoLop.GetValueOrDefault(lopToi) ?? [];
                    var ctSang = boiCanh.ChiTietTheoLop.GetValueOrDefault(lopSang) ?? [];

                    bool viPhamHoiPhuc = ctToi.Any(bt =>
                        bt.TietKetThuc >= 10 && ctSang.Any(bs =>
                            bs.Thu == bt.Thu + 1 && bs.TietBatDau <= 2 &&
                            Math.Max(bt.TuTuan, bs.TuTuan) <= Math.Min(bt.DenTuan, bs.DenTuan)));

                    if (viPhamHoiPhuc)
                    {
                        var penaltyHoiPhuc = model.NewBoolVar($"rest_{maGv}_{lopToi}_{lopSang}");
                        model.Add(xVars[(maGv, lopToi)] + xVars[(maGv, lopSang)] - penaltyHoiPhuc <= 1);
                        objectiveTerms.Add(penaltyHoiPhuc * (long)Math.Round(boiCanh.TrongSoNguyenVong * 15));
                    }
                }
            }
        }

        // w4 * P4: Phạt độ lệch chuẩn định mức giờ dạy (thiếu tải)
        long trongSoCanBang = (long)Math.Round(boiCanh.TrongSoCanBangTai * 10);
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count == 0) continue;

            bool isThinhGiang = boiCanh.GiangVienTheoMa.TryGetValue(maGv, out var gv) &&
                ((gv.ChucDanh != null && gv.ChucDanh.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase)) ||
                 gv.HoTen.Contains("Thỉnh giảng", StringComparison.OrdinalIgnoreCase));

            int minQuota = isThinhGiang ? 3 : 6;
            if (boiCanh.DinhMucTheoGv.TryGetValue(maGv, out var dm) && dm.SoTietToiThieu > 0)
            {
                minQuota = dm.SoTietToiThieu;
            }

            var dsBienGv = new List<BoolVar>();
            var dsSoTiet = new List<long>();
            foreach (var maLop in dsLopCuaGv)
            {
                dsBienGv.Add(xVars[(maGv, maLop)]);
                dsSoTiet.Add(TinhSoTiet(maLop, boiCanh));
            }

            // Phạt thiếu tải (underload)
            var underload = model.NewIntVar(0, minQuota, $"underload_{maGv}");
            model.Add(LinearExpr.WeightedSum(dsBienGv, dsSoTiet) + underload >= minQuota);
            objectiveTerms.Add(underload * trongSoCanBang);

            // Phạt vượt tải (overload) khi vượt quá định mức tối đa chuẩn
            int maxQuota = dsMaxQuotaTheoGv.GetValueOrDefault(maGv, isThinhGiang ? 16 : 25);
            var overload = model.NewIntVar(0, 50, $"overload_{maGv}");
            model.Add(LinearExpr.WeightedSum(dsBienGv, dsSoTiet) - overload <= maxQuota);
            objectiveTerms.Add(overload * (long)Math.Round(trongSoCanBang * 1.5));
        }

        // Đặt hàm mục tiêu tổng thể
        if (objectiveTerms.Count > 0)
        {
            model.Minimize(LinearExpr.Sum(objectiveTerms));
        }

        // 6. CẤU HÌNH BỘ GIẢI CP-SAT SOLVER VỚI THÔNG SỐ TỐI ƯU (Mặc định 1 luồng để đảm bảo tính tất định & nhất quán)
        var solver = new CpSolver();
        int maxWorkers = Math.Max(1, Environment.ProcessorCount);
        int numWorkers = Math.Clamp(duLieu.SoLuongWorker ?? 1, 1, Math.Min(maxWorkers, 32));
        double maxTime = Math.Clamp(duLieu.ThoiGianChayToiDa ?? 30.0, 1.0, 300.0);
        double relGap = duLieu.KhoangCachToiUuGap.HasValue ? Math.Clamp(duLieu.KhoangCachToiUuGap.Value, 0.0, 1.0) : 0.01;

        // Sử dụng CultureInfo.InvariantCulture để tránh lỗi Native Abort của C++ OR-Tools khi chạy dưới vi-VN
        solver.StringParameters = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"max_time_in_seconds:{maxTime},num_search_workers:{numWorkers},relative_gap_limit:{relGap},log_search_progress:false");

        cancellationToken.ThrowIfCancellationRequested();
        using var registration = cancellationToken.Register(() => solver.StopSearch());

        // 7. TIẾN HÀNH GIẢI BÀI TOÁN
        var stopwatch = Stopwatch.StartNew();
        var status = solver.Solve(model);
        stopwatch.Stop();

        if (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        // 8. ĐÁNH GIÁ VÀ LƯU KẾT QUẢ
        if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
        {
            var phanCongDict = new Dictionary<int, int?>();
            var dsPhanCongDto = new List<PhanCongDto>();

            // Lấy kết quả gán từ biến x[i, j]
            foreach (var ((maGv, maLop), xVar) in xVars)
            {
                if (solver.BooleanValue(xVar))
                {
                    phanCongDict[maLop] = maGv;
                    dsPhanCongDto.Add(new PhanCongDto
                    {
                        MaLopHocPhan = maLop,
                        MaGiangVien = maGv,
                        DiemPhat = 0
                    });
                }
            }

            // Đánh giá lại toàn diện bằng BoDanhGiaPhuongAn để kiểm tra ràng buộc cứng trước khi ghi CSDL
            var danhGia = boDanhGia.DanhGia(phanCongDict, boiCanh);

            // Nếu còn vi phạm ràng buộc cứng: TUYỆT ĐỐI KHÔNG GHI CSDL
            if (!danhGia.HopLe)
            {
                return new KetQuaXepLichDto
                {
                    ThuatToan = TenThuatToan,
                    TrangThai = "ConViPham",
                    TongDiemPhat = danhGia.TongDiemPhat,
                    DoThichNghi = danhGia.DoThichNghi,
                    DiemPhatRangBuocCung = danhGia.DiemPhatRangBuocCung,
                    DiemPhatNguyenVong = danhGia.DiemPhatNguyenVong,
                    DiemPhatCanBangTai = danhGia.DiemPhatCanBangTai,
                    DiemPhatGomLich = danhGia.DiemPhatGomLich,
                    SoViPhamCung = danhGia.SoViPhamCung,
                    SoLopChuaPhanCong = danhGia.SoLopChuaPhanCong,
                    ThoiGianChayGiay = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
                    ThoiGianChayMiligiay = stopwatch.ElapsedMilliseconds,
                    SoLuongBien = model.Model.Variables.Count,
                    SoLuongRangBuoc = model.Model.Constraints.Count,
                    ThongDiep = $"CP-SAT tìm thấy nghiệm nhưng còn vi phạm {danhGia.SoViPhamCung} ràng buộc cứng. Không lưu CSDL.",
                    DanhSachViPham = danhGia.DanhSachViPham,
                    ChiTietViPham = danhGia.DanhSachCanhBaoMem,
                    DanhSachPhanCong = dsPhanCongDto
                };
            }

            // Cập nhật CSDL an toàn thông qua LuuKetQuaXepLichService (có Transaction & bảo vệ lớp DaKhoa)
            var ketQuaLuu = await luuKetQuaService.LuuPhanCongAsync(
                duLieu.MaHocKy,
                TenThuatToan,
                phanCongDict,
                boiCanh,
                cancellationToken);

            string thongBaoKhoa = ketQuaLuu.SoLopGiuKhoa > 0
                ? $" ({ketQuaLuu.SoLopGiuKhoa} lớp đã khóa được giữ nguyên phân công)."
                : "";

            return new KetQuaXepLichDto
            {
                ThuatToan = TenThuatToan,
                TongDiemPhat = danhGia.TongDiemPhat,
                DoThichNghi = danhGia.DoThichNghi,
                DiemPhatRangBuocCung = danhGia.DiemPhatRangBuocCung,
                DiemPhatNguyenVong = danhGia.DiemPhatNguyenVong,
                DiemPhatCanBangTai = danhGia.DiemPhatCanBangTai,
                DiemPhatGomLich = danhGia.DiemPhatGomLich,
                SoViPhamCung = danhGia.SoViPhamCung,
                SoLopChuaPhanCong = danhGia.SoLopChuaPhanCong,
                TrangThai = status == CpSolverStatus.Optimal ? "ToiUuToanCuc" : "KhaThi",
                ThoiGianChayGiay = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
                ThoiGianChayMiligiay = stopwatch.ElapsedMilliseconds,
                SoLuongBien = model.Model.Variables.Count,
                SoLuongRangBuoc = model.Model.Constraints.Count,
                ThongDiep = (status == CpSolverStatus.Optimal
                    ? "Tìm thấy phương án tối ưu toàn cục (Optimal) thỏa mãn 100% ràng buộc cứng."
                    : "Tìm thấy phương án khả thi (Feasible) thỏa mãn 100% ràng buộc cứng.") + thongBaoKhoa,
                DanhSachPhanCong = dsPhanCongDto,
                DanhSachViPham = danhGia.DanhSachViPham,
                ChiTietViPham = danhGia.DanhSachCanhBaoMem
            };
        }

        string lyDo = status == CpSolverStatus.Infeasible
            ? "Bộ giải CP-SAT chứng minh bài toán không có nghiệm khả thi (Infeasible) do xung đột giữa các ràng buộc cứng (trùng lịch học, thiếu giảng viên đúng chuyên môn hoặc các lớp bị khóa cố định)."
            : "Bộ giải CP-SAT hết thời gian tìm kiếm mà chưa tìm thấy phương án thỏa mãn toàn bộ ràng buộc cứng.";

        return new KetQuaXepLichDto
        {
            ThuatToan = TenThuatToan,
            TongDiemPhat = 0,
            SoViPhamCung = 1,
            TrangThai = status == CpSolverStatus.Infeasible ? "VoNghiem" : "KhongTimThay",
            ThoiGianChayGiay = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
            ThoiGianChayMiligiay = stopwatch.ElapsedMilliseconds,
            SoLuongBien = model.Model.Variables.Count,
            SoLuongRangBuoc = model.Model.Constraints.Count,
            ThongDiep = lyDo,
            DanhSachViPham = [lyDo]
        };
    }

    private static int TinhSoTiet(int maLop, BoiCanhDanhGia boiCanh)
    {
        if (boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsCt))
        {
            return dsCt.Sum(ct => ct.TietKetThuc >= ct.TietBatDau ? ct.TietKetThuc - ct.TietBatDau + 1 : 0);
        }
        return 3;
    }
}
