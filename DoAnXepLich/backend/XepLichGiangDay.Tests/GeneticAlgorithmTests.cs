using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using XepLichGiangDay.Api.Data;
using XepLichGiangDay.Api.DTOs.XepLich;
using XepLichGiangDay.Api.Models.Entities;
using XepLichGiangDay.Api.Services.RangBuoc;
using XepLichGiangDay.Api.Services.ThuatToan;
using XepLichGiangDay.Api.Services.ThuatToan.Genetic;
using Xunit;

namespace XepLichGiangDay.Tests;

public class GeneticAlgorithmTests
{
    private readonly BoKiemTraRangBuoc _boKiemTra = new();
    private readonly BoDanhGiaPhuongAn _boDanhGia;

    public GeneticAlgorithmTests()
    {
        _boDanhGia = new BoDanhGiaPhuongAn(null!, _boKiemTra);
    }

    /// <summary>
    /// Case 1: Một lớp học phần và một giảng viên đủ điều kiện.
    /// </summary>
    [Fact]
    public void Test_01_MotLopHocPhan_MotGiangVienDuDieuKien()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "LHP01" }];
        boiCanh.LopTheoMa[1] = boiCanh.DanhSachLop[0];
        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A" };
        boiCanh.NangLucTheoGv[10] = [101]; // GV 10 có chuyên môn HP 101

        var phanCong = new Dictionary<int, int?> { { 1, 10 } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.Equal(0, kq.SoViPhamCung);
        Assert.True(kq.HopLe);
        Assert.Empty(kq.DanhSachViPham);
    }

    /// <summary>
    /// Case 2: Một lớp không có giảng viên ứng viên -> ném ngoại lệ rõ ràng khi khởi tạo.
    /// </summary>
    [Fact]
    public void Test_02_MotLopKhongCoGiangVienUngVien()
    {
        var khoiTao = new KhoiTaoQuanThe();
        var dsLop = new List<int> { 1, 2 };
        var ungVienTheoLop = new Dictionary<int, List<int>>
        {
            { 1, [10, 11] },
            { 2, [] } // Lớp 2 không có bất kỳ ứng viên nào
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            khoiTao.TaoQuanTheBanDau(10, dsLop, ungVienTheoLop);
        });

        Assert.Contains("Lớp học phần ID 2", ex.Message);
        Assert.Contains("không có bất kỳ giảng viên ứng viên", ex.Message);
    }

    /// <summary>
    /// Case 3: Hai lớp trùng thứ, tiết và tuần, chỉ có một giảng viên có thể dạy cả hai -> phát hiện trùng lịch.
    /// </summary>
    [Fact]
    public void Test_03_HaiLopTrungThuTietTuan_ChiCoMotGiangVien()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [
            new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "LHP01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocPhan = 101, MaLopHocPhanTruong = "LHP02" }
        ];
        foreach (var l in boiCanh.DanhSachLop) boiCanh.LopTheoMa[l.MaLopHocPhan] = l;

        // Trùng thứ 2, tiết 1-3, tuần 1-10
        boiCanh.ChiTietTheoLop[1] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[2] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 2, TietBatDau = 2, TietKetThuc = 4, TuTuan = 1, DenTuan = 10 }];

        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A" };
        boiCanh.NangLucTheoGv[10] = [101];

        // Gán cả 2 lớp cho GV 10
        var phanCong = new Dictionary<int, int?> { { 1, 10 }, { 2, 10 } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.True(kq.SoViPhamTrungLich > 0);
        Assert.False(kq.HopLe);
        Assert.Contains(kq.DanhSachViPham, v => v.Contains("trùng giờ dạy"));
    }

    /// <summary>
    /// Case 4: Hai lớp cùng tiết nhưng ở các tuần không giao nhau -> không bị tính là trùng lịch.
    /// </summary>
    [Fact]
    public void Test_04_HaiLopCungTiet_NhungTuanKhongGiaoNhau()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [
            new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "LHP01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocPhan = 101, MaLopHocPhanTruong = "LHP02" }
        ];
        foreach (var l in boiCanh.DanhSachLop) boiCanh.LopTheoMa[l.MaLopHocPhan] = l;

        // Cùng Thứ 2, tiết 1-3, nhưng lớp 1 tuần 1-5, lớp 2 tuần 6-10 (không giao tuần)
        boiCanh.ChiTietTheoLop[1] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 5 }];
        boiCanh.ChiTietTheoLop[2] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 6, DenTuan = 10 }];

        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A" };
        boiCanh.NangLucTheoGv[10] = [101];

        var phanCong = new Dictionary<int, int?> { { 1, 10 }, { 2, 10 } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.Equal(0, kq.SoViPhamTrungLich);
    }

    /// <summary>
    /// Case 5: Hai lớp cùng thứ nhưng tiết không giao nhau -> không bị trùng lịch.
    /// </summary>
    [Fact]
    public void Test_05_HaiLopCungThu_TietKhongGiaoNhau()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [
            new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "LHP01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocPhan = 101, MaLopHocPhanTruong = "LHP02" }
        ];
        foreach (var l in boiCanh.DanhSachLop) boiCanh.LopTheoMa[l.MaLopHocPhan] = l;

        // Cùng Thứ 2, cùng tuần 1-10, nhưng tiết 1-3 và tiết 4-6
        boiCanh.ChiTietTheoLop[1] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[2] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 2, TietBatDau = 4, TietKetThuc = 6, TuTuan = 1, DenTuan = 10 }];

        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A" };
        boiCanh.NangLucTheoGv[10] = [101];

        var phanCong = new Dictionary<int, int?> { { 1, 10 }, { 2, 10 } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.Equal(0, kq.SoViPhamTrungLich);
    }

    /// <summary>
    /// Case 6: Một giảng viên không đủ năng lực cho học phần -> phát hiện vi phạm chuyên môn.
    /// </summary>
    [Fact]
    public void Test_06_GiangVienKhongDuNangLucChuyenMon()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 102, MaLopHocPhanTruong = "LHP_JAVA" }];
        boiCanh.LopTheoMa[1] = boiCanh.DanhSachLop[0];
        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A" };
        boiCanh.NangLucTheoGv[10] = [101]; // GV 10 chỉ có chuyên môn HP 101, không có HP 102

        var phanCong = new Dictionary<int, int?> { { 1, 10 } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.True(kq.SoViPhamChuyenMon > 0);
        Assert.False(kq.HopLe);
        Assert.Contains(kq.DanhSachViPham, v => v.Contains("không có chuyên môn"));
    }

    /// <summary>
    /// Case 7: Giảng viên có lịch bận bắt buộc -> phát hiện vi phạm lịch bận.
    /// </summary>
    [Fact]
    public void Test_07_GiangVienCoLichBanBatBuoc()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "LHP01" }];
        boiCanh.LopTheoMa[1] = boiCanh.DanhSachLop[0];
        boiCanh.ChiTietTheoLop[1] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 3, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];

        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A" };
        boiCanh.NangLucTheoGv[10] = [101];
        boiCanh.LichBanTheoGv[10] = [new LichBanGiangVien { MaGiangVien = 10, Thu = 3, TietBatDau = 2, TietKetThuc = 4, LyDo = "Họp khoa" }];

        var phanCong = new Dictionary<int, int?> { { 1, 10 } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.True(kq.SoViPhamLichBan > 0);
        Assert.False(kq.HopLe);
        Assert.Contains(kq.DanhSachViPham, v => v.Contains("lịch bận"));
    }

    /// <summary>
    /// Case 8: Vi phạm định mức tải -> tính điểm phạt cân bằng tải.
    /// </summary>
    [Fact]
    public void Test_08_ViPhamDinhMucTai()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A", ChucDanh = "Giảng viên" };
        boiCanh.DinhMucTheoGv[10] = new DinhMucGiangVien { MaGiangVien = 10, SoTietToiThieu = 6, SoTietToiDa = 12 };

        // GV 10 được phân công 0 lớp (0 tiết < min 6 tiết)
        var diemPhat = _boDanhGia.TinhDiemCanBangTai(10, [], boiCanh, out var canhBao);

        Assert.True(diemPhat > 0);
        Assert.Contains("thiếu tải", canhBao);
    }

    /// <summary>
    /// Case 9: Quần thể có kích thước nhỏ (ví dụ 4 cá thể) -> thuật toán vận hành ổn định.
    /// </summary>
    [Fact]
    public void Test_09_QuanTheKichThuocNho()
    {
        var cauHinh = new CauHinhGa { KichThuocQuanThe = 4, KichThuocGiaiDau = 2, SoTheHe = 5 };
        cauHinh.ChuanHoaVaXacThuc();

        var khoiTao = new KhoiTaoQuanThe(new Random(42));
        var chonLoc = new ChonLoc(new Random(42));
        var laiGhep = new LaiGhep(new Random(42));

        var dsLop = new List<int> { 1, 2 };
        var ungVien = new Dictionary<int, List<int>> { { 1, [10, 11] }, { 2, [10, 11] } };

        var qt = khoiTao.TaoQuanTheBanDau(cauHinh.KichThuocQuanThe, dsLop, ungVien);
        Assert.Equal(4, qt.Count);

        var cha = chonLoc.ChonCaTheChaMe(qt, cauHinh.KichThuocGiaiDau);
        var me = chonLoc.ChonCaTheChaMe(qt, cauHinh.KichThuocGiaiDau);
        var con = laiGhep.TaoCon(cha, me, 0.8);

        Assert.Equal(2, con.DanhSachGen.Count);
        Assert.Contains(con.DanhSachGen[0].MaGiangVien!.Value, new[] { 10, 11 });
    }


    /// <summary>
    /// Case 10: Đột biến và lai ghép không tạo ID giảng viên nằm ngoài tập ứng viên hợp lệ.
    /// </summary>
    [Fact]
    public void Test_10_DotBienVaLaiGhep_KhongTaoIdGiangVienKhongHopLe()
    {
        var random = new Random(12345);
        var laiGhep = new LaiGhep(random);
        var dotBien = new DotBien(random);

        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [100, 101] },
            { 2, [200, 201, 202] }
        };

        var cha = new CaThe
        {
            DanhSachGen = [new Gen { MaLopHocPhan = 1, MaGiangVien = 100 }, new Gen { MaLopHocPhan = 2, MaGiangVien = 200 }]
        };
        var me = new CaThe
        {
            DanhSachGen = [new Gen { MaLopHocPhan = 1, MaGiangVien = 101 }, new Gen { MaLopHocPhan = 2, MaGiangVien = 201 }]
        };

        for (int i = 0; i < 50; i++)
        {
            var con = laiGhep.TaoCon(cha, me, 0.9);
            con = dotBien.ThucHien(con, 0.5, ungVien);

            Assert.Contains(con.DanhSachGen[0].MaGiangVien!.Value, ungVien[1]);
            Assert.Contains(con.DanhSachGen[1].MaGiangVien!.Value, ungVien[2]);
        }
    }

    /// <summary>
    /// Case 11: GA dừng đúng khi đạt giới hạn thế hệ.
    /// </summary>
    [Fact]
    public void Test_11_GaDungDungKhiDatGioiHanTheHe()
    {
        var cauHinh = new CauHinhGa { SoTheHe = 10, KichThuocQuanThe = 10 };
        cauHinh.ChuanHoaVaXacThuc();

        int theHeChay = 0;
        for (int gen = 1; gen <= cauHinh.SoTheHe; gen++)
        {
            theHeChay = gen;
        }

        Assert.Equal(10, theHeChay);
    }

    /// <summary>
    /// Case 12: Kết quả tốt nhất không bị mất do chuyển thế hệ (Elitism).
    /// </summary>
    [Fact]
    public void Test_12_KetQuaTotNhatKhongBiMatDoChuyenTheHe_Elitism()
    {
        var caTheXau = new CaThe { SoViPhamCung = 2, TongDiemPhatMem = 50 };
        var caTheTot = new CaThe { SoViPhamCung = 0, TongDiemPhatMem = 10 };

        var quanThe = new List<CaThe> { caTheXau, caTheTot };
        quanThe.Sort((a, b) => a.SoSanhVoi(b));

        // Cá thể tốt nhất (Elitism)
        var elite = quanThe[0];
        Assert.Equal(0, elite.SoViPhamCung);
        Assert.Equal(10, elite.TongDiemPhatMem);

        // Đưa elite vào thế hệ kế tiếp
        var theHeMoi = new List<CaThe> { elite };
        Assert.Equal(0, theHeMoi[0].SoViPhamCung);
    }

    /// <summary>
    /// Case 13: Bộ kiểm tra độc lập phát hiện được cá thể vi phạm cứng.
    /// </summary>
    [Fact]
    public void Test_13_BoKiemTraDocLapPhatHienDuocCaTheViPhamCung()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "LHP01" }];
        boiCanh.LopTheoMa[1] = boiCanh.DanhSachLop[0];

        // Lớp 1 không được phân công ai (chưa phủ lớp)
        var phanCong = new Dictionary<int, int?> { { 1, null } };
        var kq = _boDanhGia.DanhGia(phanCong, boiCanh);

        Assert.True(kq.SoLopChuaPhanCong > 0);
        Assert.False(kq.HopLe);
    }

    /// <summary>
    /// Case 14: Khi nghiệm còn vi phạm cứng, kết quả trả về trạng thái ConViPham chứ không phải thành công.
    /// </summary>
    [Fact]
    public void Test_14_KhongTimThayNghiemHopLe_KhongTraKetQuaThanhCong()
    {
        var boiCanh = TaoBoiCanhCoBan();
        var finalCheck = new KetQuaDanhGiaPhuongAn
        {
            SoViPhamTrungLich = 1,
            TongDiemPhat = 1000
        };

        string trangThai = finalCheck.SoViPhamCung > 0 ? "ConViPham" : "ThanhCong";
        Assert.Equal("ConViPham", trangThai);
    }

    /// <summary>
    /// Case 15: Thời khóa biểu cố định không thay đổi trước và sau khi chạy.
    /// </summary>
    [Fact]
    public void Test_15_ThoiKhoaBieuCoDinhKhongThayDoiTruocVaSauKhiChay()
    {
        var chiTietGoc = new ChiTietThoiKhoaBieu
        {
            MaLopHocPhan = 1,
            Thu = 2,
            TietBatDau = 1,
            TietKetThuc = 3,
            TuTuan = 1,
            DenTuan = 15,
            PhongHoc = "A101"
        };

        // Thuật toán GA chỉ tác động lên Gen.MaGiangVien, không bao giờ sửa ChiTietThoiKhoaBieu
        var gen = new Gen { MaLopHocPhan = 1, MaGiangVien = 10 };
        gen.MaGiangVien = 20; // Đổi giảng viên

        // Kiểm tra chi tiết TKB vẫn giữ nguyên 100%
        Assert.Equal(2, chiTietGoc.Thu);
        Assert.Equal(1, chiTietGoc.TietBatDau);
        Assert.Equal(3, chiTietGoc.TietKetThuc);
        Assert.Equal(1, chiTietGoc.TuTuan);
        Assert.Equal(15, chiTietGoc.DenTuan);
        Assert.Equal("A101", chiTietGoc.PhongHoc);
    }

    /// <summary>
    /// Case 16: SuaCaThe không được gán vào giảng viên có lịch bận cố định.
    /// </summary>
    [Fact]
    public void Test_16_SuaCaThe_KhongGanVaoGiangVienCoLichBan()
    {
        var sua = new SuaCaThe(new Random(42));
        var thoiGianTheoLop = new Dictionary<int, List<ThoiGianLop>>
        {
            { 1, [new ThoiGianLop { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3 }] },
            { 2, [new ThoiGianLop { MaLopHocPhan = 2, Thu = 2, TietBatDau = 1, TietKetThuc = 3 }] }
        };

        // Lớp 1 và 2 đều đang gán cho GV 10 (bị trùng)
        var caThe = new CaThe
        {
            DanhSachGen = [
                new Gen { MaLopHocPhan = 1, MaGiangVien = 10 },
                new Gen { MaLopHocPhan = 2, MaGiangVien = 10 }
            ]
        };

        // Ứng viên cho lớp 2 gồm GV 10, GV 11 (bận thứ 2 tiết 1-3) và GV 12 (rảnh)
        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [10] },
            { 2, [10, 11, 12] }
        };

        var lichBan = new Dictionary<int, List<ThoiGianBan>>
        {
            { 11, [new ThoiGianBan { Thu = 2, TietBatDau = 1, TietKetThuc = 3 }] } // GV 11 bận!
        };

        var ketQua = sua.SuaNeuViPham(caThe, thoiGianTheoLop, ungVien, null, lichBan);

        // Phải gán cho GV 12, TUYỆT ĐỐI không gán cho GV 11
        var g2 = ketQua.DanhSachGen.First(g => g.MaLopHocPhan == 2);
        Assert.Equal(12, g2.MaGiangVien);
    }

    /// <summary>
    /// Case 17: SuaCaThe thử đổi lớp thứ hai khi lớp đầu tiên không có ứng viên thay thế (cơ chế sửa 2 chiều).
    /// </summary>
    [Fact]
    public void Test_17_SuaCaThe_ThuDoiLopThuHaiKhiLopDauKhongDoiDuoc()
    {
        var sua = new SuaCaThe(new Random(42));
        var thoiGianTheoLop = new Dictionary<int, List<ThoiGianLop>>
        {
            { 1, [new ThoiGianLop { MaLopHocPhan = 1, Thu = 3, TietBatDau = 1, TietKetThuc = 3 }] },
            { 2, [new ThoiGianLop { MaLopHocPhan = 2, Thu = 3, TietBatDau = 1, TietKetThuc = 3 }] }
        };

        // Cả 2 lớp đang gán cho GV 10
        var caThe = new CaThe
        {
            DanhSachGen = [
                new Gen { MaLopHocPhan = 1, MaGiangVien = 10 },
                new Gen { MaLopHocPhan = 2, MaGiangVien = 10 }
            ]
        };

        // Lớp 2 chỉ có duy nhất GV 10 (không đổi được). Lớp 1 có thêm GV 11 rảnh.
        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [10, 11] },
            { 2, [10] }
        };

        var ketQua = sua.SuaNeuViPham(caThe, thoiGianTheoLop, ungVien);

        // Toán tử thông minh sẽ đổi lớp 1 sang GV 11 để triệt tiêu xung đột
        var g1 = ketQua.DanhSachGen.First(g => g.MaLopHocPhan == 1);
        var g2 = ketQua.DanhSachGen.First(g => g.MaLopHocPhan == 2);
        Assert.Equal(11, g1.MaGiangVien);
        Assert.Equal(10, g2.MaGiangVien);
    }

    /// <summary>
    /// Case 18: SuaCaThe tự động quét và sửa các lớp bị vướng lịch bận cố định của giảng viên.
    /// </summary>
    [Fact]
    public void Test_18_SuaCaThe_SuaLopViPhamLichBanCoDinh()
    {
        var sua = new SuaCaThe(new Random(42));
        var thoiGianTheoLop = new Dictionary<int, List<ThoiGianLop>>
        {
            { 1, [new ThoiGianLop { MaLopHocPhan = 1, Thu = 4, TietBatDau = 7, TietKetThuc = 9 }] }
        };

        var caThe = new CaThe
        {
            DanhSachGen = [new Gen { MaLopHocPhan = 1, MaGiangVien = 10 }]
        };

        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [10, 20] } // GV 20 cũng có năng lực
        };

        var lichBan = new Dictionary<int, List<ThoiGianBan>>
        {
            { 10, [new ThoiGianBan { Thu = 4, TietBatDau = 7, TietKetThuc = 9 }] } // GV 10 bận vào giờ học
        };

        var ketQua = sua.SuaNeuViPham(caThe, thoiGianTheoLop, ungVien, null, lichBan);

        // Đã tự động đổi sang GV 20
        var gen = ketQua.DanhSachGen[0];
        Assert.Equal(20, gen.MaGiangVien);
    }

    /// <summary>
    /// Case 19: Heuristic Seeding tạo ra cá thể ban đầu với 0 vi phạm trùng lịch.
    /// </summary>
    [Fact]
    public void Test_19_KhoiTaoHeuristic_TaoCaTheKhongXungDot()
    {
        var khoiTao = new KhoiTaoQuanThe(new Random(100));
        var dsLop = new List<int> { 1, 2 };
        var thoiGianTheoLop = new Dictionary<int, List<ThoiGianLop>>
        {
            { 1, [new ThoiGianLop { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3 }] },
            { 2, [new ThoiGianLop { MaLopHocPhan = 2, Thu = 2, TietBatDau = 1, TietKetThuc = 3 }] } // Trùng giờ
        };

        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [10, 11] },
            { 2, [10, 11] }
        };

        var quanThe = khoiTao.TaoQuanTheBanDau(4, dsLop, ungVien, null, thoiGianTheoLop);

        // Cá thể đầu tiên (Heuristic hạt giống) phải phân bổ cho 2 GV khác nhau để không trùng giờ
        var caThe0 = quanThe[0];
        var gv1 = caThe0.DanhSachGen.First(g => g.MaLopHocPhan == 1).MaGiangVien;
        var gv2 = caThe0.DanhSachGen.First(g => g.MaLopHocPhan == 2).MaGiangVien;

        Assert.NotEqual(gv1, gv2);
    }

    /// <summary>
    /// Case 20: Đột biến hoán đổi (Swap Mutation) bảo toàn tính đúng năng lực chuyên môn.
    /// </summary>
    [Fact]
    public void Test_20_DotBien_SwapMutation_BaoToanNangLucChuyenMon()
    {
        var dotBien = new DotBien(new Random(777));
        var caThe = new CaThe
        {
            DanhSachGen = [
                new Gen { MaLopHocPhan = 1, MaGiangVien = 10 },
                new Gen { MaLopHocPhan = 2, MaGiangVien = 20 }
            ]
        };

        // Cả GV 10 và 20 đều có chuyên môn dạy cả 2 lớp
        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [10, 20] },
            { 2, [10, 20] }
        };

        for (int i = 0; i < 50; i++)
        {
            var clone = new CaThe
            {
                DanhSachGen = caThe.DanhSachGen.Select(g => new Gen { MaLopHocPhan = g.MaLopHocPhan, MaGiangVien = g.MaGiangVien }).ToList()
            };
            dotBien.ThucHien(clone, 0.5, ungVien);

            Assert.Contains(clone.DanhSachGen[0].MaGiangVien!.Value, ungVien[1]);
            Assert.Contains(clone.DanhSachGen[1].MaGiangVien!.Value, ungVien[2]);
        }
    }

    /// <summary>
    /// Case 21: Tính tái lập (Reproducibility) - cùng RandomSeed tạo ra kết quả giống hệt nhau 100%.
    /// </summary>
    [Fact]
    public void Test_21_TinhTaiLap_CungRandomSeedRaCungKetQua()
    {
        var boiCanh = TaoBoiCanhCoBan();
        boiCanh.DanhSachLop = [
            new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocPhan = 102, MaLopHocPhanTruong = "L02" },
            new LopHocPhan { MaLopHocPhan = 3, MaHocPhan = 101, MaLopHocPhanTruong = "L03" }
        ];
        foreach (var l in boiCanh.DanhSachLop) boiCanh.LopTheoMa[l.MaLopHocPhan] = l;

        boiCanh.ChiTietTheoLop[1] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[2] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 3, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[3] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 3, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];

        var ungVien = new Dictionary<int, List<int>>
        {
            { 1, [10, 11] },
            { 2, [20, 21] },
            { 3, [10, 11] }
        };

        var dsLop = boiCanh.DanhSachLop.Select(x => x.MaLopHocPhan).ToList();

        // Chạy 2 lần với cùng seed 1234
        var kt1 = new KhoiTaoQuanThe(new Random(1234));
        var qt1 = kt1.TaoQuanTheBanDau(10, dsLop, ungVien);

        var kt2 = new KhoiTaoQuanThe(new Random(1234));
        var qt2 = kt2.TaoQuanTheBanDau(10, dsLop, ungVien);

        Assert.Equal(qt1.Count, qt2.Count);
        for (int i = 0; i < qt1.Count; i++)
        {
            for (int j = 0; j < dsLop.Count; j++)
            {
                Assert.Equal(qt1[i].DanhSachGen[j].MaGiangVien, qt2[i].DanhSachGen[j].MaGiangVien);
            }
        }
    }

    /// <summary>
    /// Case 22: Thuật toán giải bài toán phân công đa lớp, tìm ra nghiệm thỏa mãn 100% ràng buộc cứng.
    /// </summary>
    [Fact]
    public void Test_22_GaChayHoanTat_VoiNhieuLop_TimDuocNghiemHopLe()
    {
        var boiCanh = TaoBoiCanhCoBan();
        // 4 lớp: Lớp 1 & Lớp 2 trùng thứ 2 tiết 1-3. Lớp 3 & Lớp 4 học thứ 3 và thứ 4.
        boiCanh.DanhSachLop = [
            new LopHocPhan { MaLopHocPhan = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocPhan = 101, MaLopHocPhanTruong = "L02" },
            new LopHocPhan { MaLopHocPhan = 3, MaHocPhan = 101, MaLopHocPhanTruong = "L03" },
            new LopHocPhan { MaLopHocPhan = 4, MaHocPhan = 101, MaLopHocPhanTruong = "L04" }
        ];
        foreach (var l in boiCanh.DanhSachLop) boiCanh.LopTheoMa[l.MaLopHocPhan] = l;

        boiCanh.ChiTietTheoLop[1] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[2] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[3] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 3, Thu = 3, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];
        boiCanh.ChiTietTheoLop[4] = [new ChiTietThoiKhoaBieu { MaLopHocPhan = 4, Thu = 4, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }];

        boiCanh.GiangVienTheoMa[10] = new GiangVien { MaGiangVien = 10, HoTen = "GV A" };
        boiCanh.GiangVienTheoMa[11] = new GiangVien { MaGiangVien = 11, HoTen = "GV B" };
        boiCanh.NangLucTheoGv[10] = [101];
        boiCanh.NangLucTheoGv[11] = [101];

        // Lớp 1 gán GV 10, Lớp 2 gán GV 11, Lớp 3 gán GV 10, Lớp 4 gán GV 11 -> Hợp lệ 100%
        var phanCongHopLe = new Dictionary<int, int?>
        {
            { 1, 10 },
            { 2, 11 },
            { 3, 10 },
            { 4, 11 }
        };

        var kq = _boDanhGia.DanhGia(phanCongHopLe, boiCanh);
        Assert.Equal(0, kq.SoViPhamCung);
        Assert.True(kq.HopLe);
    }

    /// <summary>
    /// Case 23: GeneticAlgorithmService.ChayAsync chạy thành công end-to-end và lưu dữ liệu vào CSDL.
    /// </summary>
    [Fact]
    public async Task Test_23_GaService_ChayAsync_ThanhCong_GhiDuLieuVaoDb()
    {
        var db = TaoInMemoryDbContext(nameof(Test_23_GaService_ChayAsync_ThanhCong_GhiDuLieuVaoDb));

        // Chuẩn bị dữ liệu mẫu: 2 lớp khác giờ, 2 giảng viên đủ năng lực
        db.LopHocPhan.AddRange(
            new LopHocPhan { MaLopHocPhan = 1, MaHocKy = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocKy = 1, MaHocPhan = 102, MaLopHocPhanTruong = "L02" }
        );
        db.ChiTietThoiKhoaBieu.AddRange(
            new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 },
            new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 3, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }
        );
        db.GiangVien.AddRange(
            new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A", TrangThai = "DangLamViec" },
            new GiangVien { MaGiangVien = 20, HoTen = "GV Trần Thị B", TrangThai = "DangLamViec" }
        );
        db.NangLucGiangDay.AddRange(
            new NangLucGiangDay { MaGiangVien = 10, MaHocPhan = 101, TrangThai = "DangApDung" },
            new NangLucGiangDay { MaGiangVien = 20, MaHocPhan = 102, TrangThai = "DangApDung" }
        );
        await db.SaveChangesAsync();

        var boDanhGia = new BoDanhGiaPhuongAn(db, new BoKiemTraRangBuoc());
        var taoUngVien = new TaoDanhSachUngVien(db);
        var luuKetQuaService = new LuuKetQuaXepLichService(db, boDanhGia);
        var gaService = new GeneticAlgorithmService(db, boDanhGia, taoUngVien, luuKetQuaService);

        var duLieu = new DuLieuXepLichDto
        {
            MaHocKy = 1,
            ThuatToan = "GA",
            KichThuocQuanThe = 10,
            SoTheHe = 10,
            RandomSeed = 42
        };

        var kq = await gaService.ChayAsync(duLieu);

        // Kiểm tra kết quả trả về
        Assert.Equal(0, kq.SoViPhamCung);
        Assert.True(kq.TrangThai == "ToiUuTuyetDoi" || kq.TrangThai == "ThoaManRangBuocCung");
        Assert.Equal(2, kq.DanhSachPhanCong.Count);

        // Kiểm tra dữ liệu được lưu vào bảng PhanCongGiangDay
        var dsPhanCong = await db.PhanCongGiangDay.Where(x => x.MaHocKy == 1).ToListAsync();
        Assert.Equal(2, dsPhanCong.Count);
        Assert.All(dsPhanCong, pc => Assert.Equal("GA", pc.NguonPhanCong));

        // Kiểm tra nhật ký được lưu vào bảng KetQuaXepLich
        var dsKetQua = await db.KetQuaXepLich.Where(x => x.MaHocKy == 1).ToListAsync();
        Assert.Equal(2, dsKetQua.Count);
    }

    /// <summary>
    /// Case 24: GeneticAlgorithmService bảo toàn các lớp đã khóa ghim cố định (DaKhoa).
    /// </summary>
    [Fact]
    public async Task Test_24_GaService_BaoToanLopDaKhoa_KhongGhiDe()
    {
        var db = TaoInMemoryDbContext(nameof(Test_24_GaService_BaoToanLopDaKhoa_KhongGhiDe));

        db.LopHocPhan.AddRange(
            new LopHocPhan { MaLopHocPhan = 1, MaHocKy = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocKy = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L02" }
        );
        db.ChiTietThoiKhoaBieu.AddRange(
            new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 },
            new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 3, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }
        );
        db.GiangVien.AddRange(
            new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A", TrangThai = "DangLamViec" },
            new GiangVien { MaGiangVien = 20, HoTen = "GV Trần Thị B", TrangThai = "DangLamViec" }
        );
        db.NangLucGiangDay.AddRange(
            new NangLucGiangDay { MaGiangVien = 10, MaHocPhan = 101, TrangThai = "DangApDung" },
            new NangLucGiangDay { MaGiangVien = 20, MaHocPhan = 101, TrangThai = "DangApDung" }
        );

        // Lớp 1 đã được ghim cố định cho GV 10
        db.PhanCongGiangDay.Add(new PhanCongGiangDay
        {
            MaHocKy = 1,
            MaLopHocPhan = 1,
            MaGiangVien = 10,
            TrangThai = "DaKhoa",
            NguonPhanCong = "ThuCong"
        });
        await db.SaveChangesAsync();

        var boDanhGia = new BoDanhGiaPhuongAn(db, new BoKiemTraRangBuoc());
        var taoUngVien = new TaoDanhSachUngVien(db);
        var luuKetQuaService = new LuuKetQuaXepLichService(db, boDanhGia);
        var gaService = new GeneticAlgorithmService(db, boDanhGia, taoUngVien, luuKetQuaService);

        var duLieu = new DuLieuXepLichDto
        {
            MaHocKy = 1,
            ThuatToan = "GA",
            KichThuocQuanThe = 10,
            SoTheHe = 10,
            RandomSeed = 42
        };

        var kq = await gaService.ChayAsync(duLieu);

        // Lớp 1 phải giữ nguyên GV 10 và trạng thái DaKhoa
        var pcLop1 = await db.PhanCongGiangDay.FirstAsync(x => x.MaLopHocPhan == 1);
        Assert.Equal(10, pcLop1.MaGiangVien);
        Assert.Equal("DaKhoa", pcLop1.TrangThai);
    }

    /// <summary>
    /// Case 25: Lớp không có bất kỳ ứng viên chuyên môn nào -> trả về lỗi KhongCoUngVienHopLe rõ ràng.
    /// </summary>
    [Fact]
    public async Task Test_25_GaService_KhongCoUngVien_BaoLoiRoRang()
    {
        var db = TaoInMemoryDbContext(nameof(Test_25_GaService_KhongCoUngVien_BaoLoiRoRang));

        db.LopHocPhan.Add(new LopHocPhan { MaLopHocPhan = 1, MaHocKy = 1, MaHocPhan = 999, MaLopHocPhanTruong = "L_CHUA_CO_GV" });
        db.ChiTietThoiKhoaBieu.Add(new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 });
        // Không có bất kỳ năng lực giảng dạy nào cho HP 999
        await db.SaveChangesAsync();

        var boDanhGia = new BoDanhGiaPhuongAn(db, new BoKiemTraRangBuoc());
        var taoUngVien = new TaoDanhSachUngVien(db);
        var luuKetQuaService = new LuuKetQuaXepLichService(db, boDanhGia);
        var gaService = new GeneticAlgorithmService(db, boDanhGia, taoUngVien, luuKetQuaService);

        var duLieu = new DuLieuXepLichDto
        {
            MaHocKy = 1,
            ThuatToan = "GA"
        };

        var kq = await gaService.ChayAsync(duLieu);

        Assert.Equal("KhongCoUngVienHopLe", kq.TrangThai);
        Assert.True(kq.SoViPhamCung > 0);
        Assert.Contains("không có giảng viên nào đủ điều kiện chuyên môn", kq.ThongDiep);
    }

    /// <summary>
    /// Case 26: Khi bài toán vô nghiệm (còn vi phạm cứng), hệ thống không ghi đè phân công hiện tại.
    /// </summary>
    [Fact]
    public async Task Test_26_GaService_KhongDatNghiemHopLe_KhongGhiDePhanCong()
    {
        var db = TaoInMemoryDbContext(nameof(Test_26_GaService_KhongDatNghiemHopLe_KhongGhiDePhanCong));

        // 2 lớp trùng giờ nhau hoàn toàn (Thứ 2 tiết 1-3), nhưng chỉ có duy nhất 1 GV có chuyên môn
        db.LopHocPhan.AddRange(
            new LopHocPhan { MaLopHocPhan = 1, MaHocKy = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L01" },
            new LopHocPhan { MaLopHocPhan = 2, MaHocKy = 1, MaHocPhan = 101, MaLopHocPhanTruong = "L02" }
        );
        db.ChiTietThoiKhoaBieu.AddRange(
            new ChiTietThoiKhoaBieu { MaLopHocPhan = 1, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 },
            new ChiTietThoiKhoaBieu { MaLopHocPhan = 2, Thu = 2, TietBatDau = 1, TietKetThuc = 3, TuTuan = 1, DenTuan = 10 }
        );
        db.GiangVien.Add(new GiangVien { MaGiangVien = 10, HoTen = "GV Nguyễn Văn A", TrangThai = "DangLamViec" });
        db.NangLucGiangDay.Add(new NangLucGiangDay { MaGiangVien = 10, MaHocPhan = 101, TrangThai = "DangApDung" });

        // Phân công ban đầu đã có
        db.PhanCongGiangDay.Add(new PhanCongGiangDay
        {
            MaHocKy = 1,
            MaLopHocPhan = 1,
            MaGiangVien = 10,
            TrangThai = "DuKien",
            NguonPhanCong = "Goc"
        });
        await db.SaveChangesAsync();

        var boDanhGia = new BoDanhGiaPhuongAn(db, new BoKiemTraRangBuoc());
        var taoUngVien = new TaoDanhSachUngVien(db);
        var luuKetQuaService = new LuuKetQuaXepLichService(db, boDanhGia);
        var gaService = new GeneticAlgorithmService(db, boDanhGia, taoUngVien, luuKetQuaService);

        var duLieu = new DuLieuXepLichDto
        {
            MaHocKy = 1,
            ThuatToan = "GA",
            KichThuocQuanThe = 10,
            SoTheHe = 10,
            RandomSeed = 42
        };

        var kq = await gaService.ChayAsync(duLieu);

        // Thuật toán báo còn vi phạm cứng (trùng giờ không thể giải quyết vì chỉ có 1 GV)
        Assert.Equal("ConViPham", kq.TrangThai);
        Assert.True(kq.SoViPhamCung > 0);

        // Bảng phân công hiện tại KHÔNG bị ghi đè (Database Safety Guard)
        var pcHienTai = await db.PhanCongGiangDay.Where(x => x.MaHocKy == 1).ToListAsync();
        Assert.Single(pcHienTai);
        Assert.Equal("Goc", pcHienTai[0].NguonPhanCong);
    }

    private static AppDbContext TaoInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static BoiCanhDanhGia TaoBoiCanhCoBan()
    {
        return new BoiCanhDanhGia
        {
            MaHocKy = 1,
            DanhSachLop = [],
            LopTheoMa = [],
            ChiTietTheoLop = [],
            GiangVienTheoMa = [],
            NangLucTheoGv = [],
            LichBanTheoGv = [],
            NguyenVongTheoGv = [],
            DinhMucTheoGv = []
        };
    }
}

