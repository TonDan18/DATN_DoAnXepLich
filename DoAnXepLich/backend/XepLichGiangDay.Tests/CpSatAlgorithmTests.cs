using System.Globalization;
using Google.OrTools.Sat;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;
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
        var cpSatService = new CpSatAlgorithmService(context, boDanhGia, boKiemTra);

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
}
