using Microsoft.AspNetCore.Mvc;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Services;
using XepLichGiangDay.Api.Services.ThuatToan;

namespace XepLichGiangDay.Api.Controllers;

[ApiController]
[Route("api/xep-lich")]
public class XepLichController(
    IEnumerable<ISchedulingAlgorithm> algorithms,
    XepLichService xepLichService,
    HocKyLockService lockService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<KetQuaXepLichDto>> ChayXepLich([FromBody] YeuCauXepLichDto request, CancellationToken cancellationToken)
    {
        var thuatToan = request.ThuatToan.Trim().ToUpperInvariant();
        var algorithm = algorithms.FirstOrDefault(x => x.TenThuatToan == thuatToan);

        if (algorithm is null)
        {
            return BadRequest("Thuat toan chi ho tro GA hoac CP_SAT.");
        }

        var sem = lockService.GetLock(request.MaHocKy);
        if (!await sem.WaitAsync(0, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Xung đột tiến trình xếp lịch",
                Detail = $"Học kỳ {request.MaHocKy} đang có một tiến trình xếp lịch đang chạy. Vui lòng đợi hoàn tất trước khi gửi yêu cầu mới.",
                Status = StatusCodes.Status409Conflict
            });
        }

        try
        {
            var duLieu = new DuLieuXepLichDto
            {
                MaHocKy = request.MaHocKy,
                ThuatToan = thuatToan,
                KichThuocQuanThe = request.KichThuocQuanThe,
                SoTheHe = request.SoTheHe,
                TyLeLaiGhep = request.TyLeLaiGhep,
                TyLeDotBien = request.TyLeDotBien,
                ThoiGianChayToiDa = request.ThoiGianChayToiDa,
                SoLuongWorker = request.SoLuongWorker,
                KhoangCachToiUuGap = request.KhoangCachToiUuGap,
                RandomSeed = request.RandomSeed,
                KichThuocGiaiDau = request.KichThuocGiaiDau,
                SoCaTheUuTu = request.SoCaTheUuTu
            };

            var ketQua = await algorithm.ChayAsync(duLieu, cancellationToken);
            return Ok(ketQua);
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Lấy bảng phân công giảng dạy chi tiết theo học kỳ.
    /// </summary>
    [HttpGet("bang-phan-cong/{maHocKy:int}")]
    public async Task<ActionResult<BangPhanCongHocKyDto>> LayBangPhanCong(int maHocKy, CancellationToken cancellationToken)
    {
        var ketQua = await xepLichService.LayBangPhanCongAsync(maHocKy, cancellationToken);
        return Ok(ketQua);
    }

    /// <summary>
    /// Can thiệp phân công hoặc thay đổi giảng viên thủ công cho lớp học phần.
    /// </summary>
    [HttpPost("phan-cong-thu-cong")]
    public async Task<ActionResult<KetQuaPhanCongThuCongDto>> PhanCongThuCong([FromBody] YeuCauPhanCongThuCongDto req, CancellationToken cancellationToken)
    {
        var ketQua = await xepLichService.PhanCongThuCongAsync(req, cancellationToken);
        return Ok(ketQua);
    }

    /// <summary>
    /// Đổi trạng thái khóa / ghim phân công của một lớp học phần.
    /// </summary>
    [HttpPut("khoa/{maLopHocPhan:int}")]
    public async Task<IActionResult> DoiTrangThaiKhoa(int maLopHocPhan, [FromQuery] bool daKhoa, CancellationToken cancellationToken)
    {
        var thanhCong = await xepLichService.DoiTrangThaiKhoaAsync(maLopHocPhan, daKhoa, cancellationToken);
        if (!thanhCong)
        {
            return NotFound("Không tìm thấy thông tin phân công của lớp học phần này.");
        }
        return Ok(new { thanhCong = true, daKhoa });
    }

    /// <summary>
    /// Hủy phân công của lớp học phần (trở về trạng thái chưa phân công).
    /// </summary>
    [HttpDelete("huy-phan-cong/{maLopHocPhan:int}")]
    public async Task<IActionResult> HuyPhanCong(int maLopHocPhan, CancellationToken cancellationToken)
    {
        var thanhCong = await xepLichService.HuyPhanCongAsync(maLopHocPhan, cancellationToken);
        if (!thanhCong)
        {
            return NotFound("Lớp học phần này chưa được phân công hoặc không tồn tại.");
        }
        return Ok(new { thanhCong = true });
    }

    /// <summary>
    /// Thống kê khối lượng giảng dạy của toàn bộ giảng viên trong kỳ và so sánh định mức.
    /// </summary>
    [HttpGet("thong-ke-tai/{maHocKy:int}")]
    public async Task<ActionResult<List<ThongKeTaiGvDto>>> LayThongKeTai(int maHocKy, CancellationToken cancellationToken)
    {
        var ketQua = await xepLichService.LayThongKeTaiAsync(maHocKy, cancellationToken);
        return Ok(ketQua);
    }

    /// <summary>
    /// Xuất toàn bộ bảng phân công thời khóa biểu và thống kê tải ra file Excel (.xlsx).
    /// </summary>
    [HttpGet("xuat-excel/{maHocKy:int}")]
    public async Task<IActionResult> XuatExcel(int maHocKy, CancellationToken cancellationToken)
    {
        var bytes = await xepLichService.XuatExcelBangPhanCongAsync(maHocKy, cancellationToken);
        var tenFile = $"ThoiKhoaBieu_PhanCong_HocKy_{maHocKy}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", tenFile);
    }

    /// <summary>
    /// Chạy đối đầu thực nghiệm 2 thuật toán CP-SAT và GA trên cùng một học kỳ để so sánh trên Dashboard.
    /// </summary>
    [HttpPost("so-sanh-thuat-toan")]
    public async Task<ActionResult<SoSanhThuatToanDto>> SoSanhThuatToan([FromBody] YeuCauXepLichDto request, CancellationToken cancellationToken)
    {
        var cpSatAlgo = algorithms.FirstOrDefault(x => x.TenThuatToan == "CP_SAT");
        var gaAlgo = algorithms.FirstOrDefault(x => x.TenThuatToan == "GA");

        if (cpSatAlgo == null || gaAlgo == null)
        {
            return BadRequest("Không tìm thấy đủ 2 thuật toán CP-SAT và GA trong hệ thống.");
        }

        var sem = lockService.GetLock(request.MaHocKy);
        if (!await sem.WaitAsync(0, cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Xung đột tiến trình xếp lịch",
                Detail = $"Học kỳ {request.MaHocKy} đang có tiến trình khác thực thi. Vui lòng thử lại sau.",
                Status = StatusCodes.Status409Conflict
            });
        }

        try
        {
            var tongQuan = await xepLichService.LayThongTinTongQuanHocKyAsync(request.MaHocKy, cancellationToken);

            // 1. Chạy CP-SAT
            var duLieuCpSat = new DuLieuXepLichDto
            {
                MaHocKy = request.MaHocKy,
                ThuatToan = "CP_SAT",
                ThoiGianChayToiDa = request.ThoiGianChayToiDa > 0 ? request.ThoiGianChayToiDa : 15,
                SoLuongWorker = 1
            };
            var kqCpSat = await cpSatAlgo.ChayAsync(duLieuCpSat, cancellationToken);

            // 2. Chạy GA
            var duLieuGa = new DuLieuXepLichDto
            {
                MaHocKy = request.MaHocKy,
                ThuatToan = "GA",
                KichThuocQuanThe = request.KichThuocQuanThe > 0 ? request.KichThuocQuanThe : 50,
                SoTheHe = request.SoTheHe > 0 ? request.SoTheHe : 100,
                TyLeLaiGhep = request.TyLeLaiGhep > 0 ? request.TyLeLaiGhep : 0.8,
                TyLeDotBien = request.TyLeDotBien > 0 ? request.TyLeDotBien : 0.15
            };
            var kqGa = await gaAlgo.ChayAsync(duLieuGa, cancellationToken);

            tongQuan.KetQuaCpSat = kqCpSat;
            tongQuan.KetQuaGa = kqGa;

            return Ok(tongQuan);
        }
        finally
        {
            sem.Release();
        }
    }
}