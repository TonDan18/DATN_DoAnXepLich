using System.Text;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Models.Entities;

namespace XepLichGiangDay.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<NamHoc> NamHoc => Set<NamHoc>();
    public DbSet<HocKy> HocKy => Set<HocKy>();
    public DbSet<BoMon> BoMon => Set<BoMon>();
    public DbSet<GiangVien> GiangVien => Set<GiangVien>();
    public DbSet<HocPhan> HocPhan => Set<HocPhan>();
    public DbSet<LopHocPhan> LopHocPhan => Set<LopHocPhan>();
    public DbSet<ChiTietThoiKhoaBieu> ChiTietThoiKhoaBieu => Set<ChiTietThoiKhoaBieu>();
    public DbSet<NangLucGiangDay> NangLucGiangDay => Set<NangLucGiangDay>();
    public DbSet<LichBanGiangVien> LichBanGiangVien => Set<LichBanGiangVien>();
    public DbSet<NguyenVongGiangVien> NguyenVongGiangVien => Set<NguyenVongGiangVien>();
    public DbSet<DinhMucGiangVien> DinhMucGiangVien => Set<DinhMucGiangVien>();
    public DbSet<RangBuocHocKy> RangBuocHocKy => Set<RangBuocHocKy>();
    public DbSet<PhanCongGiangDay> PhanCongGiangDay => Set<PhanCongGiangDay>();
    public DbSet<KetQuaXepLich> KetQuaXepLich => Set<KetQuaXepLich>();
    public DbSet<NguoiDung> NguoiDung => Set<NguoiDung>();
    public DbSet<LichSuImport> LichSuImport => Set<LichSuImport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. NamHoc
        modelBuilder.Entity<NamHoc>(b =>
        {
            b.ToTable("nam_hoc").HasKey(x => x.MaNamHoc);
        });

        // 2. HocKy (1 NamHoc - N HocKy)
        modelBuilder.Entity<HocKy>(b =>
        {
            b.ToTable("hoc_ky").HasKey(x => x.MaHocKy);
            b.HasOne<NamHoc>()
             .WithMany()
             .HasForeignKey(x => x.MaNamHoc)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 0. BoMon
        modelBuilder.Entity<BoMon>(b =>
        {
            b.ToTable("bo_mon").HasKey(x => x.MaBoMon);
        });

        // 3. GiangVien
        modelBuilder.Entity<GiangVien>(b =>
        {
            b.ToTable("giang_vien").HasKey(x => x.MaGiangVien);
            b.HasOne<BoMon>()
             .WithMany()
             .HasForeignKey(x => x.MaBoMon)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. HocPhan
        modelBuilder.Entity<HocPhan>(b =>
        {
            b.ToTable("hoc_phan").HasKey(x => x.MaHocPhan);
            b.HasOne<BoMon>()
             .WithMany()
             .HasForeignKey(x => x.MaBoMon)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. LopHocPhan (1 HocPhan - N LopHocPhan, 1 HocKy - N LopHocPhan)
        modelBuilder.Entity<LopHocPhan>(b =>
        {
            b.ToTable("lop_hoc_phan").HasKey(x => x.MaLopHocPhan);
            b.HasOne<HocPhan>()
             .WithMany()
             .HasForeignKey(x => x.MaHocPhan)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. ChiTietThoiKhoaBieu (1 LopHocPhan - N ChiTietThoiKhoaBieu)
        modelBuilder.Entity<ChiTietThoiKhoaBieu>(b =>
        {
            b.ToTable("chi_tiet_thoi_khoa_bieu").HasKey(x => x.MaChiTiet);
            b.HasOne<LopHocPhan>()
             .WithMany()
             .HasForeignKey(x => x.MaLopHocPhan)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // 7. NangLucGiangDay (N GiangVien - N HocPhan)
        modelBuilder.Entity<NangLucGiangDay>(b =>
        {
            b.ToTable("nang_luc_giang_day").HasKey(x => x.MaNangLuc);
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<HocPhan>()
             .WithMany()
             .HasForeignKey(x => x.MaHocPhan)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // 8. LichBanGiangVien (1 GiangVien - N LichBan, 1 HocKy - N LichBan)
        modelBuilder.Entity<LichBanGiangVien>(b =>
        {
            b.ToTable("lich_ban_giang_vien").HasKey(x => x.MaLichBan);
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. NguyenVongGiangVien (1 GiangVien - N NguyenVong, 1 HocKy - N NguyenVong)
        modelBuilder.Entity<NguyenVongGiangVien>(b =>
        {
            b.ToTable("nguyen_vong_giang_vien").HasKey(x => x.MaNguyenVong);
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 10. DinhMucGiangVien (1 GiangVien - N DinhMuc, 1 HocKy - N DinhMuc)
        modelBuilder.Entity<DinhMucGiangVien>(b =>
        {
            b.ToTable("dinh_muc_giang_vien").HasKey(x => x.MaDinhMuc);
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 11. RangBuocHocKy (1 HocKy - N RangBuoc)
        modelBuilder.Entity<RangBuocHocKy>(b =>
        {
            b.ToTable("rang_buoc_hoc_ky").HasKey(x => x.MaRangBuoc);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // 12. PhanCongGiangDay (1 LopHocPhan - N PhanCong, 1 GiangVien - N PhanCong, 1 HocKy - N PhanCong)
        modelBuilder.Entity<PhanCongGiangDay>(b =>
        {
            b.ToTable("phan_cong_giang_day").HasKey(x => x.MaPhanCong);
            b.HasIndex(x => x.MaLopHocPhan).IsUnique();
            b.HasOne<LopHocPhan>()
             .WithMany()
             .HasForeignKey(x => x.MaLopHocPhan)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 13. KetQuaXepLich (1 HocKy - N KetQua, 1 LopHocPhan - N KetQua, 1 GiangVien - N KetQua)
        modelBuilder.Entity<KetQuaXepLich>(b =>
        {
            b.ToTable("ket_qua_xep_lich").HasKey(x => x.MaKetQua);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasOne<LopHocPhan>()
             .WithMany()
             .HasForeignKey(x => x.MaLopHocPhan)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // 14. NguoiDung (Tai khoan he thong)
        modelBuilder.Entity<NguoiDung>(b =>
        {
            b.ToTable("nguoi_dung").HasKey(x => x.MaNguoiDung);
            b.HasIndex(x => x.TenDangNhap).IsUnique();
            b.HasOne<GiangVien>()
             .WithMany()
             .HasForeignKey(x => x.MaGiangVien)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // 15. LichSuImport
        modelBuilder.Entity<LichSuImport>(b =>
        {
            b.ToTable("lich_su_import").HasKey(x => x.MaLichSu);
            b.HasOne<HocKy>()
             .WithMany()
             .HasForeignKey(x => x.MaHocKy)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Tự động chuyển toàn bộ tên cột sang snake_case
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var builder = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && input[i - 1] != '_')
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
}
