-- ==============================================================================
-- SCRIPT NẠP DỮ LIỆU SEED MẪU CHUẨN CHO ĐỒ ÁN XẾP LỊCH GIẢNG DẠY
-- ==============================================================================

USE [XepLichGiangDay];
GO

-- 1. BỘ MÔN
IF NOT EXISTS (SELECT 1 FROM [bo_mon] WHERE [ten_bo_mon] = N'Bộ môn Công nghệ Thông tin')
BEGIN
    INSERT INTO [bo_mon] ([ten_bo_mon], [ten_khoa])
    VALUES (N'Bộ môn Công nghệ Thông tin', N'Khoa Công nghệ Thông tin');
END
DECLARE @MaBoMon INT = (SELECT TOP 1 [ma_bo_mon] FROM [bo_mon] WHERE [ten_bo_mon] = N'Bộ môn Công nghệ Thông tin');

-- 2. NĂM HỌC
IF NOT EXISTS (SELECT 1 FROM [nam_hoc] WHERE [ten_nam_hoc] = N'2026-2027')
BEGIN
    INSERT INTO [nam_hoc] ([ten_nam_hoc], [ngay_bat_dau], [ngay_ket_thuc], [trang_thai])
    VALUES (N'2026-2027', '2026-09-01', '2027-06-30', N'DangDienRa');
END
DECLARE @MaNamHoc INT = (SELECT TOP 1 [ma_nam_hoc] FROM [nam_hoc] WHERE [ten_nam_hoc] = N'2026-2027');

-- 3. HỌC KỲ
IF NOT EXISTS (SELECT 1 FROM [hoc_ky] WHERE [ten_hoc_ky] = N'Học kỳ 1 (2026-2027)')
BEGIN
    INSERT INTO [hoc_ky] ([ma_nam_hoc], [ten_hoc_ky], [ngay_bat_dau], [ngay_ket_thuc], [trang_thai])
    VALUES (@MaNamHoc, N'Học kỳ 1 (2026-2027)', '2026-09-05', '2027-01-20', N'DangMo');
END
DECLARE @MaHocKy INT = (SELECT TOP 1 [ma_hoc_ky] FROM [hoc_ky] WHERE [ten_hoc_ky] = N'Học kỳ 1 (2026-2027)');

-- 4. HỌC PHẦN (MÔN HỌC)
DECLARE @HocPhanTable TABLE (Ten NVARCHAR(200), SoTinChi INT);
INSERT INTO @HocPhanTable VALUES
(N'Cơ sở dữ liệu', 3),
(N'Lập trình hướng đối tượng', 3),
(N'Cấu trúc dữ liệu và giải thuật', 3),
(N'Phát triển ứng dụng Web', 3),
(N'Mạng máy tính', 3),
(N'Trí tuệ nhân tạo', 3),
(N'Kiến trúc máy tính và Hệ điều hành', 3),
(N'Công nghệ phần mềm', 3);

INSERT INTO [hoc_phan] ([ten_hoc_phan], [so_tin_chi], [ma_bo_mon], [trang_thai])
SELECT src.Ten, src.SoTinChi, @MaBoMon, N'DangApDung'
FROM @HocPhanTable src
WHERE NOT EXISTS (SELECT 1 FROM [hoc_phan] WHERE [ten_hoc_phan] = src.Ten);

-- 5. GIẢNG VIÊN, NĂNG LỰC GIẢNG DẠY & ĐỊNH MỨC:
-- Để trống theo yêu cầu của người dùng (người dùng sẽ tự nhập danh sách giảng viên thực tế sau).

-- 8. LỚP HỌC PHẦN & THỜI KHÓA BIỂU CỦA TRƯỜNG (10 lớp học phần mẫu)
-- Lớp 1: CSDL Nhóm 1
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_CSDL_01')
BEGIN
    DECLARE @HpCSDL INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Cơ sở dữ liệu');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_CSDL_01', @HpCSDL, @MaHocKy, N'Cơ sở dữ liệu - Nhóm 01', 65, N'DangMo');
    DECLARE @Lhp1 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp1, 2, 1, 3, N'301.A1', 1, 20);
END

-- Lớp 2: CSDL Nhóm 2
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_CSDL_02')
BEGIN
    DECLARE @HpCSDL2 INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Cơ sở dữ liệu');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_CSDL_02', @HpCSDL2, @MaHocKy, N'Cơ sở dữ liệu - Nhóm 02', 60, N'DangMo');
    DECLARE @Lhp2 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp2, 3, 7, 9, N'302.A1', 1, 20);
END

-- Lớp 3: OOP Nhóm 1
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_OOP_01')
BEGIN
    DECLARE @HpOOP INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Lập trình hướng đối tượng');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_OOP_01', @HpOOP, @MaHocKy, N'Lập trình hướng đối tượng - 01', 70, N'DangMo');
    DECLARE @Lhp3 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp3, 2, 7, 9, N'205.H1', 1, 20);
END

-- Lớp 4: OOP Nhóm 2
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_OOP_02')
BEGIN
    DECLARE @HpOOP2 INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Lập trình hướng đối tượng');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_OOP_02', @HpOOP2, @MaHocKy, N'Lập trình hướng đối tượng - 02', 65, N'DangMo');
    DECLARE @Lhp4 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp4, 4, 1, 3, N'206.H1', 1, 20);
END

-- Lớp 5: CTDL & GT Nhóm 1
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_CTDL_01')
BEGIN
    DECLARE @HpCTDL INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Cấu trúc dữ liệu và giải thuật');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_CTDL_01', @HpCTDL, @MaHocKy, N'Cấu trúc dữ liệu và giải thuật - 01', 60, N'DangMo');
    DECLARE @Lhp5 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp5, 3, 1, 3, N'401.A1', 1, 20);
END

-- Lớp 6: Web Nhóm 1
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_WEB_01')
BEGIN
    DECLARE @HpWeb INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Phát triển ứng dụng Web');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_WEB_01', @HpWeb, @MaHocKy, N'Phát triển ứng dụng Web - 01', 50, N'DangMo');
    DECLARE @Lhp6 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp6, 4, 7, 9, N'PM1.TT', 1, 20);
END

-- Lớp 7: Mạng máy tính Nhóm 1
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_MMT_01')
BEGIN
    DECLARE @HpMMT INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Mạng máy tính');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_MMT_01', @HpMMT, @MaHocKy, N'Mạng máy tính - 01', 55, N'DangMo');
    DECLARE @Lhp7 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp7, 5, 1, 3, N'201.A1', 1, 20);
END

-- Lớp 8: Trí tuệ nhân tạo Nhóm 1
IF NOT EXISTS (SELECT 1 FROM [lop_hoc_phan] WHERE [ma_lop_hoc_phan_truong] = N'261_AI_01')
BEGIN
    DECLARE @HpAI INT = (SELECT TOP 1 [ma_hoc_phan] FROM [hoc_phan] WHERE [ten_hoc_phan] = N'Trí tuệ nhân tạo');
    INSERT INTO [lop_hoc_phan] ([ma_lop_hoc_phan_truong], [ma_hoc_phan], [ma_hoc_ky], [ten_lop], [so_luong_sinh_vien], [trang_thai])
    VALUES (N'261_AI_01', @HpAI, @MaHocKy, N'Trí tuệ nhân tạo - 01', 45, N'DangMo');
    DECLARE @Lhp8 INT = SCOPE_IDENTITY();
    INSERT INTO [chi_tiet_thoi_khoa_bieu] ([ma_lop_hoc_phan], [thu], [tiet_bat_dau], [tiet_ket_thuc], [phong_hoc], [tu_tuan], [den_tuan])
    VALUES (@Lhp8, 6, 1, 3, N'305.A1', 1, 20);
END

-- 9. RÀNG BUỘC HỌC KỲ
IF NOT EXISTS (SELECT 1 FROM [rang_buoc_hoc_ky] WHERE [ma_hoc_ky] = @MaHocKy)
BEGIN
    INSERT INTO [rang_buoc_hoc_ky] ([ma_hoc_ky], [ten_rang_buoc], [loai_rang_buoc], [trong_so_phat], [mo_ta])
    VALUES
    (@MaHocKy, N'Vi phạm lịch bận cố định', N'Cung', 1000.0, N'Giảng viên bị xếp dạy vào khung giờ bận cố định'),
    (@MaHocKy, N'Trùng lịch dạy', N'Cung', 1000.0, N'Giảng viên bị xếp dạy 2 lớp trùng thời gian'),
    (@MaHocKy, N'Sai chuyên môn', N'Cung', 1000.0, N'Giảng viên dạy môn không thuộc danh mục năng lực'),
    (@MaHocKy, N'Lệch định mức giờ dạy', N'Mem', 15.0, N'Giảng viên dạy dưới mức tối thiểu hoặc vượt tối đa'),
    (@MaHocKy, N'Không thỏa mãn nguyện vọng', N'Mem', 10.0, N'Xếp vào ca tránh hoặc không trúng ca mong muốn'),
    (@MaHocKy, N'Rải lịch dạy trong tuần', N'Mem', 25.0, N'Giảng viên phải đi nhiều ngày nhưng dạy ít tiết');
END
GO
