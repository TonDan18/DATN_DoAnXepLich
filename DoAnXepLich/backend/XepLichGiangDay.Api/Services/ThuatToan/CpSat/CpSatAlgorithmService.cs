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

            // Nếu một lớp không có bất kỳ giảng viên hợp lệ nào: KHÔNG hủy toàn bộ bài toán.
            // Lớp này sẽ tự động bị bỏ trống (ungVienTheoLop rỗng => Sum(ứng viên) = 0),
            // ràng buộc H5 "mềm hóa" ở bước sau cho phép bienBoTrong = 1 trong trường hợp này.
        }

        // Tiền xử lý số tiết theo từng tuần của từng lớp học phần
        var dsTuan = new HashSet<int>();
        var soTietTheoTuanTheoLop = new Dictionary<int, Dictionary<int, int>>();
        var tongTietHocKyTheoLop = new Dictionary<int, int>();

        foreach (var lop in boiCanh.DanhSachLop)
        {
            int maLop = lop.MaLopHocPhan;
            var tietTheoTuan = new Dictionary<int, int>();
            int tongTietHk = 0;

            if (boiCanh.ChiTietTheoLop.TryGetValue(maLop, out var dsCt) && dsCt.Count > 0)
            {
                foreach (var ct in dsCt)
                {
                    int soTietBuoi = ct.TietKetThuc >= ct.TietBatDau ? ct.TietKetThuc - ct.TietBatDau + 1 : 0;
                    int tuTuan = ct.TuTuan > 0 ? ct.TuTuan : 1;
                    int denTuan = ct.DenTuan >= tuTuan ? ct.DenTuan : tuTuan;

                    for (int w = tuTuan; w <= denTuan; w++)
                    {
                        dsTuan.Add(w);
                        tietTheoTuan[w] = tietTheoTuan.GetValueOrDefault(w) + soTietBuoi;
                    }
                    tongTietHk += soTietBuoi * (denTuan - tuTuan + 1);
                }
            }

            soTietTheoTuanTheoLop[maLop] = tietTheoTuan;
            tongTietHocKyTheoLop[maLop] = tongTietHk;
        }

        // 4. Thiết lập HỆ THỐNG 6 RÀNG BUỘC CỨNG (HARD CONSTRAINTS)
        // Danh sách các term của hàm mục tiêu (khai báo sớm để dùng ngay trong ràng buộc H5 "mềm hóa")
        var objectiveTerms = new List<LinearExpr>();

        // (H5) Phủ kín lớp học phần: ưu tiên tuyệt đối mỗi lớp có đúng 1 giảng viên,
        // nhưng KHÔNG bắt buộc cứng — nếu lớp xung đột không thể giải (ví dụ nhiều lớp
        // trùng giờ nhưng chỉ có 1 GV đủ chuyên môn), cho phép bỏ trống lớp đó thay vì
        // làm toàn bộ bài toán Infeasible. Mức phạt "bỏ trống" cực lớn để solver chỉ
        // chấp nhận bỏ trống khi thực sự không còn cách nào khác thỏa các ràng buộc cứng khác.
        const long TRONG_SO_BO_TRONG = 10_000_000L;
        foreach (var lop in boiCanh.DanhSachLop)
        {
            var dsBienLop = ungVienTheoLop[lop.MaLopHocPhan].Select(g => xVars[(g, lop.MaLopHocPhan)]).ToList();
            var bienBoTrong = model.NewBoolVar($"botrong_l{lop.MaLopHocPhan}");

            // Sum(ứng viên) + bienBoTrong == 1: hoặc có đúng 1 GV dạy, hoặc lớp bị bỏ trống
            model.Add(LinearExpr.Sum(dsBienLop) + bienBoTrong == 1);
            objectiveTerms.Add(bienBoTrong * TRONG_SO_BO_TRONG);
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

        // (H4) Khống chế tải tối đa theo từng tuần thực tế (Weekly Load Hard Cap)
        // Thay vì cộng dồn cả kỳ gây vô nghiệm giả khi dạy tách đợt (ví dụ 14 lớp chia đều 2 nửa kỳ),
        // khống chế trần tải trên TỪNG TUẦN học cụ thể.
        var dinhMucTheoGv = new Dictionary<int, (int MinTiet, int MaxTiet)>();
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count == 0) continue;

            var (minQuota, maxQuota) = BoDanhGiaPhuongAn.LayDinhMucTuan(maGv, boiCanh);
            dinhMucTheoGv[maGv] = (minQuota, maxQuota);

            // Ngưỡng trần cứng theo tuần: bảo vệ sức khỏe giảng viên (tối đa Math.Max(maxQuota + 12, 35) tiết/tuần)
            int tranCungTuan = Math.Max(maxQuota + 12, 35);

            if (dsTuan.Count > 0)
            {
                foreach (var w in dsTuan)
                {
                    var dsBienTuan = new List<BoolVar>();
                    var dsSoTietTuan = new List<long>();

                    foreach (var maLop in dsLopCuaGv)
                    {
                        int tietLopTuan = soTietTheoTuanTheoLop[maLop].GetValueOrDefault(w, 0);
                        if (tietLopTuan > 0)
                        {
                            dsBienTuan.Add(xVars[(maGv, maLop)]);
                            dsSoTietTuan.Add(tietLopTuan);
                        }
                    }

                    if (dsBienTuan.Count > 0)
                    {
                        model.Add(LinearExpr.WeightedSum(dsBienTuan, dsSoTietTuan) <= tranCungTuan);
                    }
                }
            }
            else
            {
                var dsBienGv = dsLopCuaGv.Select(maLop => xVars[(maGv, maLop)]).ToList();
                var dsSoTiet = dsLopCuaGv.Select(maLop => (long)TinhSoTiet(maLop, boiCanh)).ToList();
                model.Add(LinearExpr.WeightedSum(dsBienGv, dsSoTiet) <= tranCungTuan);
            }
        }

        // 5. BỔ SUNG HÀM MỤC TIÊU VỚI CÁC RÀNG BUỘC MỀM KHÁC (SOFT CONSTRAINTS)
        // Tối thiểu hóa tổng chi phí phạt: Z_penalty (objectiveTerms đã được khai báo và
        // nạp phạt "bỏ trống lớp" ở bước H5 phía trên)

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

            for (int thu = 2; thu <= 8; thu++)
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

        // w4 * P4: Phạt độ lệch chuẩn định mức giờ dạy (quá tải tuần cao điểm & thiếu tải trung bình)
        long trongSoCanBang = (long)Math.Round(boiCanh.TrongSoCanBangTai * 10);
        foreach (var (maGv, dsLopCuaGv) in lopTheoGv)
        {
            if (dsLopCuaGv.Count == 0) continue;

            var (minQuota, maxQuota) = dinhMucTheoGv.GetValueOrDefault(maGv, BoDanhGiaPhuongAn.LayDinhMucTuan(maGv, boiCanh));

            // Biến peakLoad theo dõi tải tuần cao điểm nhất (max week load)
            var peakLoad = model.NewIntVar(0, 100, $"peak_{maGv}");

            if (dsTuan.Count > 0)
            {
                foreach (var w in dsTuan)
                {
                    var dsBienTuan = new List<BoolVar>();
                    var dsSoTietTuan = new List<long>();

                    foreach (var maLop in dsLopCuaGv)
                    {
                        int tietLopTuan = soTietTheoTuanTheoLop[maLop].GetValueOrDefault(w, 0);
                        if (tietLopTuan > 0)
                        {
                            dsBienTuan.Add(xVars[(maGv, maLop)]);
                            dsSoTietTuan.Add(tietLopTuan);
                        }
                    }

                    if (dsBienTuan.Count > 0)
                    {
                        model.Add(peakLoad >= LinearExpr.WeightedSum(dsBienTuan, dsSoTietTuan));
                    }
                }
            }
            else
            {
                var dsBienGv = dsLopCuaGv.Select(maLop => xVars[(maGv, maLop)]).ToList();
                var dsSoTiet = dsLopCuaGv.Select(maLop => (long)TinhSoTiet(maLop, boiCanh)).ToList();
                model.Add(peakLoad >= LinearExpr.WeightedSum(dsBienGv, dsSoTiet));
            }

            // Phạt vượt tải (overload) khi tuần cao điểm vượt quá maxQuota (hệ số 1.5 giống BoDanhGiaPhuongAn)
            var overload = model.NewIntVar(0, 50, $"overload_{maGv}");
            model.Add(overload >= peakLoad - maxQuota);
            objectiveTerms.Add(overload * (long)Math.Round(trongSoCanBang * 1.5));

            // Phạt thiếu tải (underload) so với minQuota
            // Quy đổi tổng tiết học kỳ về tuần (chia 15 tuần chuẩn): minSemester = minQuota * 15
            var dsBienTatCaLop = dsLopCuaGv.Select(maLop => xVars[(maGv, maLop)]).ToList();
            var dsTongTietHk = dsLopCuaGv.Select(maLop => (long)(tongTietHocKyTheoLop.GetValueOrDefault(maLop, 0) > 0 
                ? tongTietHocKyTheoLop[maLop] 
                : TinhSoTiet(maLop, boiCanh) * 15)).ToList();

            int minTietHocKy = minQuota * 15;
            var underloadHocKy = model.NewIntVar(0, minTietHocKy, $"underload_{maGv}");
            model.Add(LinearExpr.WeightedSum(dsBienTatCaLop, dsTongTietHk) + underloadHocKy >= minTietHocKy);

            // underloadHocKy / 15 tương đương underload tuần; nhân với trongSoCanBang
            long trongSoUnderload = (long)Math.Max(1, Math.Round(boiCanh.TrongSoCanBangTai * 10.0 / 15.0));
            objectiveTerms.Add(underloadHocKy * trongSoUnderload);
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

            // Các lớp bị bỏ trống (biến botrong = true, hoặc lớp không có ứng viên nào từ đầu):
            // đánh dấu rõ ràng bằng null để BoDanhGia tính đúng SoLopChuaPhanCong và
            // LuuKetQuaXepLichService xóa đúng phân công cũ (nếu có) của lớp này.
            foreach (var lop in boiCanh.DanhSachLop)
            {
                if (!phanCongDict.ContainsKey(lop.MaLopHocPhan))
                {
                    phanCongDict[lop.MaLopHocPhan] = null;
                }
            }

            // Đánh giá lại toàn diện bằng BoDanhGiaPhuongAn để kiểm tra ràng buộc cứng trước khi ghi CSDL
            var danhGia = boDanhGia.DanhGia(phanCongDict, boiCanh);

            // Chỉ chặn ghi CSDL khi còn vi phạm ràng buộc cứng THẬT (sai chuyên môn, trùng lịch,
            // vướng lịch bận cố định). Lớp bị bỏ trống do xung đột không thể giải (SoLopChuaPhanCong)
            // KHÔNG chặn việc lưu — các lớp đã xếp được vẫn phải được ghi nhận, lớp bỏ trống chỉ cảnh báo.
            int soViPhamCungThat = danhGia.SoViPhamChuyenMon + danhGia.SoViPhamTrungLich + danhGia.SoViPhamLichBan;
            if (soViPhamCungThat > 0)
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
                    ThongDiep = $"CP-SAT tìm thấy nghiệm nhưng còn vi phạm {soViPhamCungThat} ràng buộc cứng (sai chuyên môn/trùng lịch/lịch bận). Không lưu CSDL.",
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

            string thongBaoBoTrong = danhGia.SoLopChuaPhanCong > 0
                ? $" CẢNH BÁO: {danhGia.SoLopChuaPhanCong} lớp học phần bị BỎ TRỐNG (chưa có giảng viên) do xung đột không thể giải (ví dụ nhiều lớp trùng giờ nhưng không đủ giảng viên đúng chuyên môn); các lớp còn lại đã được phân công đầy đủ và lưu CSDL."
                : "";

            string trangThaiKetQua = status == CpSolverStatus.Optimal ? "ToiUuToanCuc" : "KhaThi";
            if (danhGia.SoLopChuaPhanCong > 0)
            {
                trangThaiKetQua = "KhaThiMotPhan";
            }

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
                TrangThai = trangThaiKetQua,
                ThoiGianChayGiay = Math.Round(stopwatch.Elapsed.TotalSeconds, 2),
                ThoiGianChayMiligiay = stopwatch.ElapsedMilliseconds,
                SoLuongBien = model.Model.Variables.Count,
                SoLuongRangBuoc = model.Model.Constraints.Count,
                ThongDiep = (status == CpSolverStatus.Optimal
                    ? "Tìm thấy phương án tối ưu toàn cục (Optimal) thỏa mãn các ràng buộc cứng còn lại."
                    : "Tìm thấy phương án khả thi (Feasible) thỏa mãn các ràng buộc cứng còn lại.") + thongBaoKhoa + thongBaoBoTrong,
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
        return 0;
    }
}
