export interface YeuCauXepLich {
  maHocKy: number
  thuatToan: 'GA' | 'CP_SAT'
  kichThuocQuanThe?: number
  soTheHe?: number
  tyLeLaiGhep?: number
  tyLeDotBien?: number
  thoiGianChayToiDa?: number
  soLuongWorker?: number
}

export interface PhanCong {
  maLopHocPhan: number
  maGiangVien: number
  diemPhat: number
  ghiChu: string
}

export interface KetQuaXepLich {
  thuatToan: string
  tongDiemPhat: number
  doThichNghi?: number
  diemPhatRangBuocCung?: number
  diemPhatNguyenVong?: number
  diemPhatCanBangTai?: number
  diemPhatGomLich?: number
  soViPhamCung?: number
  soLopChuaPhanCong?: number
  soTheHeThucTe?: number
  thoiGianChayMiligiay?: number
  trangThai: string
  lichSuDiemPhatTheoTheHe?: number[]
  danhSachViPham?: string[]
  danhSachPhanCong: PhanCong[]
  thoiGianChayGiay?: number
  soLuongBien?: number
  soLuongRangBuoc?: number
  thongDiep?: string
  chiTietViPham?: string[]
}

export interface SoSanhThuatToan {
  maHocKy: number
  tenHocKy: string
  tongSoLop: number
  tongSoGiangVien: number
  ketQuaCpSat: KetQuaXepLich
  ketQuaGa: KetQuaXepLich
}

export interface UngVienLop {
  maGiangVien: number
  hoTen: string
  email: string
  chucDanh?: string
  coNangLuc: boolean
  biVuongLichBan: boolean
  biTrungLichDay: boolean
  hopLe: boolean
  lyDoKhongHopLe: string
}

export interface DongPhanCongLop {
  maLopHocPhan: number
  maLopHocPhanTruong: string
  maHocPhan: number
  tenHocPhan: string
  soTinChi: number
  tenLop: string
  soLuongSinhVien: number
  thu: number
  tietBatDau: number
  tietKetThuc: number
  soTiet: number
  phongHoc: string
  tuTuan: number
  denTuan: number
  maPhanCong?: number
  maGiangVien?: number
  tenGiangVien?: string
  emailGiangVien?: string
  nguonPhanCong: string
  trangThai: string
  daKhoa: boolean
  diemPhat: number
  danhSachUngVien: UngVienLop[]
}

export interface BangPhanCongHocKy {
  maHocKy: number
  tenHocKy: string
  tongSoLop: number
  soLopDaPhanCong: number
  soLopChuaPhanCong: number
  soLopDaKhoa: number
  danhSachLop: DongPhanCongLop[]
}

export interface YeuCauPhanCongThuCong {
  maLopHocPhan: number
  maGiangVien: number
  khoaNgay?: boolean
  boQuaCanhBao?: boolean
}

export interface KetQuaPhanCongThuCong {
  thanhCong: boolean
  thongBao: string
  danhSachViPham: string[]
  canXacNhan: boolean
}

export interface ThongKeTaiGv {
  maGiangVien: number
  hoTen: string
  email: string
  chucDanh?: string
  soLopPhanCong: number
  tongSoTinChi: number
  tongSoTietTuan: number
  soTietToiThieu: number
  soTietToiDa: number
  trangThaiTai: 'ThieuTai' | 'BinhThuong' | 'QuaTai'
}
