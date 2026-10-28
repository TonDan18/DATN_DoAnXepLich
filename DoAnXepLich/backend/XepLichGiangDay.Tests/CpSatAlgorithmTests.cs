using System.Globalization;
using Google.OrTools.Sat;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;
using XepLichGiangDay.Api.Services.ThuatToan;
using XepLichGiangDay.Api.Services.ThuatToan.CpSat;
using XepLichGiangDay.Api.Services.ThuatToan.Genetic;
using Xunit;

namespace XepLichGiangDay.Tests;

public class CpSatAlgorithmTests
{
    [Fact]
    public void Test_CpSat_CultureInfo_ViVN_KhongCrashKhiThoiGianLaSoLe()
    {
        // Lưu lại Culture ban đầu
        var oldCulture = CultureInfo.CurrentCulture;
        var oldUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            // Thiết lập culture tiếng Việt (dấu phẩy thập phân: 2,5)
            var viVn = new CultureInfo("vi-VN");
            CultureInfo.CurrentCulture = viVn;
            CultureInfo.CurrentUICulture = viVn;

            double maxTime = 2.5;
            int numWorkers = 1;
            double relGap = 0.01;

            var solver = new CpSolver();

            // Định dạng InvariantCulture chuẩn xác
            solver.StringParameters = string.Create(
                CultureInfo.InvariantCulture,
                $"max_time_in_seconds:{maxTime},num_search_workers:{numWorkers},relative_gap_limit:{relGap},log_search_progress:false");

            var model = new CpModel();
            var x = model.NewBoolVar("x");
            model.Add(x == 1);

            // Phải giải thành công không bị native abort crash C++
            var status = solver.Solve(model);
            Assert.Equal(CpSolverStatus.Optimal, status);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    [Fact]
    public void Test_CauHinhGa_GioiHanCanTrenTranhDoS()
    {
        var cauHinh = new CauHinhGa
        {
            KichThuocQuanThe = 50_000_000,
            SoTheHe = 999_999,
            ThoiGianChayToiDaGiay = 86400,
            TyLeLaiGhep = 5.0,
            TyLeDotBien = -1.0,
            KichThuocGiaiDau = 100,
            SoCaTheUuTu = 200
        };

        cauHinh.ChuanHoaVaXacThuc();

        Assert.Equal(500, cauHinh.KichThuocQuanThe);
        Assert.Equal(5000, cauHinh.SoTheHe);
        Assert.Equal(300.0, cauHinh.ThoiGianChayToiDaGiay);
        Assert.Equal(1.0, cauHinh.TyLeLaiGhep);
        Assert.Equal(0.0, cauHinh.TyLeDotBien);
        Assert.True(cauHinh.KichThuocGiaiDau <= 50);
        Assert.True(cauHinh.SoCaTheUuTu <= 50);
    }

    [Fact]
    public async Task Test_CpSat_CancellationToken_HuyTimKiem()
    {
        var dbName = "CpSat_Cancel_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new AppDbContext(options);
        var boKiemTra = new BoKiemTraRangBuoc();
        var boDanhGia = new BoDanhGiaPhuongAn(context, boKiemTra);
        var luuKetQuaService = new LuuKetQuaXepLichService(context, boDanhGia);
        var cpSatService = new CpSatAlgorithmService(context, boDanhGia, boKiemTra, luuKetQuaService);

        // Tạo 1 lớp học để không bị trả về sớm KhongCoLop
        context.LopHocPhan.Add(new LopHocPhan
        {
            MaLopHocPhan = 1,
            MaHocKy = 1,
            MaHocPhan = 101,
            MaLopHocPhanTruong = "LHP01",
            TenLop = "Lop 1"
        });
        await context.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Hủy ngay trước khi giải

        var duLieu = new DuLieuXepLichDto
        {
            MaHocKy = 1,
            ThoiGianChayToiDa = 2.5
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await cpSatService.ChayAsync(duLieu, cts.Token);
        });
    }

    [Fact]
    public void Test_CpSat_LayDinhMucTuan_ChuanHoaDinhMucHocKySangTuan()
    {
        var boiCanh = new BoiCanhDanhGia();
        boiCanh.GiangVienTheoMa[1] = new GiangVien
        {
            MaGiangVien = 1,
            HoTen = "GV Cơ Hữu",
            ChucDanh = "Giảng viên"
        };
        boiCanh.GiangVienTheoMa[2] = new GiangVien
        {
            MaGiangVien = 2,
            HoTen = "GV Thỉnh Giảng",
            ChucDanh = "Thỉnh giảng"
        };

        // GV 1: Khai báo định mức theo kỳ (> 30) -> 90 tiết tối thiểu, 300 tiết tối đa
        boiCanh.DinhMucTheoGv[1] = new DinhMucGiangVien
        {
            MaGiangVien = 1,
            SoTietToiThieu = 90,
            SoTietToiDa = 300
        };

        var (min1, max1) = BoDanhGiaPhuongAn.LayDinhMucTuan(1, boiCanh);
        Assert.Equal(6, min1);   // 90 / 15 = 6
        Assert.Equal(25, max1);  // Khong ap tran hoc ky, toi da tuan co huu la 25 tiet

        // GV 2: Khai báo định mức theo tuần (<= 30) -> 4 tiết tối thiểu, 14 tiết tối đa
        boiCanh.DinhMucTheoGv[2] = new DinhMucGiangVien
        {
            MaGiangVien = 2,
            SoTietToiThieu = 4,
            SoTietToiDa = 14
        };

        var (min2, max2) = BoDanhGiaPhuongAn.LayDinhMucTuan(2, boiCanh);
        Assert.Equal(4, min2);
        Assert.Equal(14, max2);
    }

    [Fact]
    public void Test_CpSat_GiangVienCoHuu12TietTuan_KhongBiQuaTai()
    {
        var boiCanh = new BoiCanhDanhGia();
        boiCanh.GiangVienTheoMa[1] = new GiangVien { MaGiangVien = 1, HoTen = "Lê Văn Minh", ChucDanh = "Giảng viên" };
        boiCanh.DinhMucTheoGv[1] = new DinhMucGiangVien { MaGiangVien = 1, SoTietToiThieu = 60, SoTietToiDa = 120 };

        var (minTiet, maxTiet) = BoDanhGiaPhuongAn.LayDinhMucTuan(1, boiCanh);
        Assert.Equal(25, maxTiet);

        boiCanh.ChiTietTheoLop[1] = [new() { MaChiTiet = 1, MaLopHocPhan = 1, Thu = 2, TietBatDau = 7, TietKetThuc = 9, TuTuan = 1, DenTuan = 8 }];
        boiCanh.ChiTietTheoLop[2] = [new() { MaChiTiet = 2, MaLopHocPhan = 2, Thu = 2, TietBatDau = 10, TietKetThuc = 12, TuTuan = 1, DenTuan = 8 }];
        boiCanh.ChiTietTheoLop[3] = [new() { MaChiTiet = 3, MaLopHocPhan = 3, Thu = 3, TietBatDau = 4, TietKetThuc = 6, TuTuan = 1, DenTuan = 8 }];
        boiCanh.ChiTietTheoLop[4] = [new() { MaChiTiet = 4, MaLopHocPhan = 4, Thu = 5, TietBatDau = 10, TietKetThuc = 12, TuTuan = 1, DenTuan = 8 }];

        var boKiemTra = new BoKiemTraRangBuoc();
        var boDanhGia = new BoDanhGiaPhuongAn(null!, boKiemTra);

        double diemPhat = boDanhGia.TinhDiemCanBangTai(1, [1, 2, 3, 4], boiCanh, out string canhBao);
        Assert.Equal(0, diemPhat);
        Assert.Empty(canhBao);
    }

    [Fact]
    public async Task Test_CpSat_LopKhongCoUngVien_TraVeVoNghiemCoViPhamCung()
    {
        var dbName = "CpSat_NoCand_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new AppDbContext(options);
        var boKiemTra = new BoKiemTraRangBuoc();
        var boDanhGia = new BoDanhGiaPhuongAn(context, boKiemTra);
        var luuKetQuaService = new LuuKetQuaXepLichService(context, boDanhGia);
        var cpSatService = new CpSatAlgorithmService(context, boDanhGia, boKiemTra, luuKetQuaService);

        context.LopHocPhan.Add(new LopHocPhan
        {
            MaLopHocPhan = 1,
            MaHocKy = 1,
            MaHocPhan = 999, // Môn không có ai có năng lực
            MaLopHocPhanTruong = "LHP_KHONG_GV",
            TenLop = "Lớp Không Có Giảng Viên"
        });
        context.GiangVien.Add(new GiangVien
        {
            MaGiangVien = 1,
            HoTen = "Thầy A",
            TrangThai = "DangLamViec"
        });
        // Không thêm bản ghi NangLucGiangDay nào
        await context.SaveChangesAsync();

        var result = await cpSatService.ChayAsync(new DuLieuXepLichDto { MaHocKy = 1, ThoiGianChayToiDa = 5.0 });

        Assert.Equal("KhaThiMotPhan", result.TrangThai);
        Assert.Equal(1, result.SoViPhamCung);
        Assert.Equal(1, result.SoLopChuaPhanCong);
        Assert.NotEmpty(result.DanhSachViPham);
    }

    [Fact]
    public async Task Test_CpSat_ChiaNuaKy_KhongBiVoNghiemGia()
    {
        // Kịch bản: 14 lớp học phần, mỗi lớp 3 tiết.
        // 7 lớp dạy nửa kỳ đầu (tuần 1-8), 7 lớp dạy nửa kỳ sau (tuần 9-16).
        // Tổng số tiết cả kỳ = 14 * 3 = 42 tiết (> trần cũ 40).
        // Nhưng mỗi tuần chỉ dạy đúng 21 tiết (<= định mức trần 25 tiết/tuần).
        // Thuật toán CP-SAT mới tính theo tuần phải giải thành công và tìm ra nghiệm khả thi/tối ưu!
        var dbName = "CpSat_SplitSemester_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new AppDbContext(options);
        var boKiemTra = new BoKiemTraRangBuoc();
        var boDanhGia = new BoDanhGiaPhuongAn(context, boKiemTra);
        var luuKetQuaService = new LuuKetQuaXepLichService(context, boDanhGia);
        var cpSatService = new CpSatAlgorithmService(context, boDanhGia, boKiemTra, luuKetQuaService);

        int maHocKy = 1;
        int maHp = 10;
        int maGv = 1;

        context.HocKy.Add(new HocKy { MaHocKy = maHocKy, TenHocKy = "HK1" });
        context.GiangVien.Add(new GiangVien
        {
            MaGiangVien = maGv,
            HoTen = "GV Nguyễn Văn A",
            ChucDanh = "Giảng viên",
            TrangThai = "DangLamViec"
        });
        context.NangLucGiangDay.Add(new NangLucGiangDay
        {
            MaGiangVien = maGv,
            MaHocPhan = maHp,
            TrangThai = "DangApDung"
        });

        // 7 slot lịch không trùng nhau:
        // Slot 1: Thứ 2 tiết 1-3
        // Slot 2: Thứ 2 tiết 4-6
        // Slot 3: Thứ 3 tiết 1-3
        // Slot 4: Thứ 3 tiết 4-6
        // Slot 5: Thứ 4 tiết 1-3
        // Slot 6: Thứ 4 tiết 4-6
        // Slot 7: Thứ 5 tiết 1-3
        var slots = new (int Thu, int Tbd, int Tkt)[]
        {
            (2, 1, 3),
            (2, 4, 6),
            (3, 1, 3),
            (3, 4, 6),
            (4, 1, 3),
            (4, 4, 6),
            (5, 1, 3)
        };

        // 7 lớp tuần 1-8
        for (int i = 0; i < 7; i++)
        {
            int maLop = i + 1;
            context.LopHocPhan.Add(new LopHocPhan
            {
                MaLopHocPhan = maLop,
                MaHocKy = maHocKy,
                MaHocPhan = maHp,
                MaLopHocPhanTruong = $"LHP_D1_{i + 1}",
                TenLop = $"Lớp Đợt 1 - {i + 1}"
            });
            context.ChiTietThoiKhoaBieu.Add(new ChiTietThoiKhoaBieu
            {
                MaChiTiet = maLop,
                MaLopHocPhan = maLop,
                Thu = slots[i].Thu,
                TietBatDau = slots[i].Tbd,
                TietKetThuc = slots[i].Tkt,
                TuTuan = 1,
                DenTuan = 8
            });
        }

        // 7 lớp tuần 9-16 (cùng slot nhưng khác tuần)
        for (int i = 0; i < 7; i++)
        {
            int maLop = i + 8;
            context.LopHocPhan.Add(new LopHocPhan
            {
                MaLopHocPhan = maLop,
                MaHocKy = maHocKy,
                MaHocPhan = maHp,
                MaLopHocPhanTruong = $"LHP_D2_{i + 1}",
                TenLop = $"Lớp Đợt 2 - {i + 1}"
            });
            context.ChiTietThoiKhoaBieu.Add(new ChiTietThoiKhoaBieu
            {
                MaChiTiet = maLop,
                MaLopHocPhan = maLop,
                Thu = slots[i].Thu,
                TietBatDau = slots[i].Tbd,
                TietKetThuc = slots[i].Tkt,
                TuTuan = 9,
                DenTuan = 16
            });
        }

        await context.SaveChangesAsync();

        var result = await cpSatService.ChayAsync(new DuLieuXepLichDto
        {
            MaHocKy = maHocKy,
            ThoiGianChayToiDa = 5.0
        });

        // Kết quả phải thành công (không bị vô nghiệm giả do tổng tiết 42 > 40)
        Assert.True(result.TrangThai == "ToiUuToanCuc" || result.TrangThai == "KhaThi");
        Assert.Equal(0, result.SoViPhamCung);
        Assert.Equal(14, result.DanhSachPhanCong.Count);
        Assert.All(result.DanhSachPhanCong, pc => Assert.Equal(maGv, pc.MaGiangVien));
    }

    [Fact]
    public async Task Test_CpSat_HoTroXepLichNgayChuNhat()
    {
        var dbName = "CpSat_Sunday_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new AppDbContext(options);
        var boKiemTra = new BoKiemTraRangBuoc();
        var boDanhGia = new BoDanhGiaPhuongAn(context, boKiemTra);
        var luuKetQuaService = new LuuKetQuaXepLichService(context, boDanhGia);
        var cpSatService = new CpSatAlgorithmService(context, boDanhGia, boKiemTra, luuKetQuaService);

        int maHocKy = 1;
        int maHp = 10;
        int maGv = 1;

        context.GiangVien.Add(new GiangVien
        {
            MaGiangVien = maGv,
            HoTen = "GV Dạy Chủ Nhật",
            ChucDanh = "Giảng viên",
            TrangThai = "DangLamViec"
        });
        context.NangLucGiangDay.Add(new NangLucGiangDay
        {
            MaGiangVien = maGv,
            MaHocPhan = maHp,
            TrangThai = "DangApDung"
        });
        context.LopHocPhan.Add(new LopHocPhan
        {
            MaLopHocPhan = 1,
            MaHocKy = maHocKy,
            MaHocPhan = maHp,
            MaLopHocPhanTruong = "LHP_CN",
            TenLop = "Lớp Chủ Nhật"
        });
        context.ChiTietThoiKhoaBieu.Add(new ChiTietThoiKhoaBieu
        {
            MaChiTiet = 1,
            MaLopHocPhan = 1,
            Thu = 8, // Chủ Nhật
            TietBatDau = 1,
            TietKetThuc = 3,
            TuTuan = 1,
            DenTuan = 15
        });

        await context.SaveChangesAsync();

        var result = await cpSatService.ChayAsync(new DuLieuXepLichDto
        {
            MaHocKy = maHocKy,
            ThoiGianChayToiDa = 5.0
        });

        Assert.True(result.TrangThai == "ToiUuToanCuc" || result.TrangThai == "KhaThi");
        Assert.Equal(0, result.SoViPhamCung);
        Assert.Single(result.DanhSachPhanCong);
        Assert.Equal(maGv, result.DanhSachPhanCong[0].MaGiangVien);
    }
}
