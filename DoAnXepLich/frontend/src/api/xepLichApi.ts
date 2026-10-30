import axiosClient from './axiosClient'
import type {
  BangPhanCongHocKy,
  KetQuaPhanCongThuCong,
  KetQuaXepLich,
  SoSanhThuatToan,
  ThongKeTaiGv,
  YeuCauPhanCongThuCong,
  YeuCauXepLich,
} from '../types/xepLich'

export const xepLichApi = {
  chayXepLich: (data: YeuCauXepLich) =>
    axiosClient.post<KetQuaXepLich>('/xep-lich', data),

  soSanhThuatToan: (data: Partial<YeuCauXepLich>) =>
    axiosClient.post<SoSanhThuatToan>('/xep-lich/so-sanh-thuat-toan', data),

  layBangPhanCong: (maHocKy: number) =>
    axiosClient.get<BangPhanCongHocKy>(`/xep-lich/bang-phan-cong/${maHocKy}`),

  phanCongThuCong: (data: YeuCauPhanCongThuCong) =>
    axiosClient.post<KetQuaPhanCongThuCong>('/xep-lich/phan-cong-thu-cong', data),

  doiTrangThaiKhoa: (maLopHocPhan: number, daKhoa: boolean) =>
    axiosClient.put<{ thanhCong: boolean; daKhoa: boolean }>(
      `/xep-lich/khoa/${maLopHocPhan}?daKhoa=${daKhoa}`
    ),

  huyPhanCong: (maLopHocPhan: number) =>
    axiosClient.delete<{ thanhCong: boolean }>(
      `/xep-lich/huy-phan-cong/${maLopHocPhan}`
    ),

  layThongKeTai: (maHocKy: number) =>
    axiosClient.get<ThongKeTaiGv[]>(`/xep-lich/thong-ke-tai/${maHocKy}`),

  xuatExcel: (maHocKy: number) =>
    axiosClient.get(`/xep-lich/xuat-excel/${maHocKy}`, {
      responseType: 'blob',
    }),
}
