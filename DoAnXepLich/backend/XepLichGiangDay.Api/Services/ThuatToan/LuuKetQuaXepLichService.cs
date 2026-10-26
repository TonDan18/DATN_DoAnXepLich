using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;

namespace XepLichGiangDay.Api.Services.ThuatToan;

public class KetQuaLuuPhanCong
{
    public bool ThanhCong { get; set; }
    public int SoLopCapNhat { get; set; }
    public int SoLopThemMoi { get; set; }
    public int SoLopGiuKhoa { get; set; }
    public List<PhanCongGiangDay> DanhSachPhanCong { get; set; } = [];
}

/// <summary>
/// Service dùng chung cho cả GA và CP-SAT để lưu kết quả phân công an toàn vào CSDL:
/// - Mở Transaction bảo vệ tính toàn vẹn.
/// - Đọc lại phân công mới nhất ngay trước khi ghi.
/// - Bảo vệ tuyệt đối các lớp đã ở trạng thái "DaKhoa" (không bị đè phân công nếu bị khóa trong khi thuật toán đang chạy).
/// - Đồng bộ lịch sử vào bảng ket_qua_xep_lich cho mọi thuật toán.
/// </summary>
public class LuuKetQuaXepLichService(
    AppDbContext context,
    BoDanhGiaPhuongAn boDanhGia)
{
    public async Task<KetQuaLuuPhanCong> LuuPhanCongAsync(
        int maHocKy,
        string tenThuatToan,
        Dictionary<int, int?> phanCongLop,
        BoiCanhDanhGia boiCanh,
        CancellationToken cancellationToken = default)
    {
        var ketQua = new KetQuaLuuPhanCong();
        bool isRelational = context.Database.IsRelational();
        await using var transaction = isRelational ? await context.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            // 1. Đọc lại danh sách phân công mới nhất từ CSDL ngay trước khi ghi
            var dsPhanCongHienTai = await context.PhanCongGiangDay
                .Where(x => x.MaHocKy == maHocKy)
                .ToListAsync(cancellationToken);

            var mapPc = dsPhanCongHienTai
                .GroupBy(x => x.MaLopHocPhan)
                .ToDictionary(g => g.Key, g => g.First());

            var dsPhanCongKetQua = new List<PhanCongGiangDay>();

            foreach (var (maLop, maGvOpt) in phanCongLop)
            {
                if (!maGvOpt.HasValue || maGvOpt.Value <= 0) continue;
                int maGv = maGvOpt.Value;
                double diemPhat = Math.Round(boDanhGia.TinhDiemNguyenVong(maGv, maLop, boiCanh), 1);

                if (mapPc.TryGetValue(maLop, out var pc))
                {
                    // Nếu lớp đã ở trạng thái DaKhoa: giữ nguyên phân công và khóa, không ghi đè
                    if (pc.TrangThai == "DaKhoa")
                    {
                        ketQua.SoLopGiuKhoa++;
                        dsPhanCongKetQua.Add(pc);
                        continue;
                    }

                    pc.MaGiangVien = maGv;
                    pc.NguonPhanCong = tenThuatToan;
                    pc.TrangThai = "DuKien";
                    pc.DiemPhat = diemPhat;
                    ketQua.SoLopCapNhat++;
                    dsPhanCongKetQua.Add(pc);
                }
                else
                {
                    var pcMoi = new PhanCongGiangDay
                    {
                        MaHocKy = maHocKy,
                        MaLopHocPhan = maLop,
                        MaGiangVien = maGv,
                        NguonPhanCong = tenThuatToan,
                        TrangThai = "DuKien",
                        DiemPhat = diemPhat
                    };
                    context.PhanCongGiangDay.Add(pcMoi);
                    ketQua.SoLopThemMoi++;
                    dsPhanCongKetQua.Add(pcMoi);
                }
            }

            // Giữ lại các phân công khác trong kỳ (ví dụ các lớp không nằm trong phanCongLop nhưng đang DaKhoa)
            foreach (var pc in dsPhanCongHienTai)
            {
                if (!phanCongLop.ContainsKey(pc.MaLopHocPhan) && !dsPhanCongKetQua.Contains(pc))
                {
                    dsPhanCongKetQua.Add(pc);
                }
            }

            // 2. Ghi nhật ký vào ket_qua_xep_lich cho cả GA và CP-SAT
            var oldKetQua = await context.KetQuaXepLich
                .Where(x => x.MaHocKy == maHocKy)
                .ToListAsync(cancellationToken);
            context.KetQuaXepLich.RemoveRange(oldKetQua);

            var dsKetQuaMoi = dsPhanCongKetQua.Select(pc => new KetQuaXepLich
            {
                MaHocKy = maHocKy,
                MaLopHocPhan = pc.MaLopHocPhan,
                MaGiangVien = pc.MaGiangVien,
                DiemPhat = pc.DiemPhat,
                GhiChu = $"{tenThuatToan} lúc {DateTime.Now:dd/MM/yyyy HH:mm:ss}"
            }).ToList();
            context.KetQuaXepLich.AddRange(dsKetQuaMoi);

            await context.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            ketQua.ThanhCong = true;
            ketQua.DanhSachPhanCong = dsPhanCongKetQua;
            return ketQua;
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            throw;
        }
    }
}
