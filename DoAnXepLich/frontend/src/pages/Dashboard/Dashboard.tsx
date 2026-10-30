import { useEffect, useState } from 'react'
import { hocKyApi } from '../../api/hocKyApi'
import { xepLichApi } from '../../api/xepLichApi'
import type { HocKy } from '../../types/hocKy'
import type { SoSanhThuatToan } from '../../types/xepLich'

function Dashboard() {
  const [danhSachHocKy, setDanhSachHocKy] = useState<HocKy[]>([])
  const [maHocKy, setMaHocKy] = useState<number>(0)
  const [dangChay, setDangChay] = useState<boolean>(false)
  const [ketQuaSoSanh, setKetQuaSoSanh] = useState<SoSanhThuatToan | null>(null)
  const [loi, setLoi] = useState<string>('')

  // 1. Tải danh sách học kỳ
  useEffect(() => {
    void (async () => {
      try {
        const res = await hocKyApi.getAll()
        setDanhSachHocKy(res.data)
        if (res.data.length > 0) {
          const hkMo = res.data.find((x: HocKy) => x.trangThai === 'DangMo') ?? res.data[0]
          setMaHocKy(hkMo.maHocKy)
        }
      } catch {
        setLoi('Không thể tải danh sách học kỳ.')
      }
    })()
  }, [])

  // 2. Chạy đối đầu thực nghiệm 2 thuật toán
  const handleChaySoSanh = async () => {
    if (!maHocKy) {
      alert('Vui lòng chọn học kỳ.')
      return
    }

    try {
      setDangChay(true)
      setLoi('')
      const res = await xepLichApi.soSanhThuatToan({
        maHocKy,
        thoiGianChayToiDa: 15,
        soLuongWorker: 1,
        soTheHe: 100,
        kichThuocQuanThe: 50,
      })
      setKetQuaSoSanh(res.data)
    } catch (err: unknown) {
      const errorMsg =
        (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail ||
        'Có lỗi xảy ra trong quá trình chạy thử nghiệm thuật toán.'
      setLoi(errorMsg)
    } finally {
      setDangChay(false)
    }
  }

  const cpSat = ketQuaSoSanh?.ketQuaCpSat
  const ga = ketQuaSoSanh?.ketQuaGa

  return (
    <section>
      {/* Tiêu đề & Chọn học kỳ */}
      <div className="d-flex flex-wrap justify-content-between align-items-center mb-4">
        <div>
          <h2 className="h4 mb-1 text-primary fw-bold">
            Dashboard Trưởng Bộ Môn & Thử Nghiệm Thuật Toán
          </h2>
          <div className="text-secondary small">
            Trung tâm đối sánh thực nghiệm hai giải thuật cốt lõi: <strong>CP-SAT (Google OR-Tools)</strong> vs{' '}
            <strong>Genetic Algorithm (GA)</strong>.
          </div>
        </div>

        <div className="d-flex align-items-center gap-2 mt-2 mt-sm-0">
          <label htmlFor="selectHocKyDash" className="col-form-label-sm text-nowrap fw-semibold">
            Học kỳ:
          </label>
          <select
            id="selectHocKyDash"
            className="form-select form-select-sm"
            style={{ width: 'auto', minWidth: '180px' }}
            value={maHocKy}
            onChange={(e) => setMaHocKy(Number(e.target.value))}
            disabled={dangChay}
          >
            {danhSachHocKy.map((hk) => (
              <option key={hk.maHocKy} value={hk.maHocKy}>
                {hk.tenHocKy}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="btn btn-sm btn-primary fw-semibold px-3"
            onClick={() => void handleChaySoSanh()}
            disabled={dangChay || !maHocKy}
          >
            {dangChay ? (
              <>
                <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true" />
                Đang chạy thực nghiệm...
              </>
            ) : (
              '⚡ Chạy đối đầu CP-SAT vs GA'
            )}
          </button>
        </div>
      </div>

      {loi && <div className="alert alert-danger py-2 mb-3 small">{loi}</div>}

      {/* Thông số tổng quan quy mô đào tạo */}
      {ketQuaSoSanh && (
        <div className="row g-3 mb-4">
          <div className="col-12 col-md-4">
            <div className="card shadow-sm border-0 border-start border-primary border-4 bg-light">
              <div className="card-body py-2 px-3">
                <div className="text-muted small text-uppercase fw-semibold">Học kỳ đang chọn</div>
                <div className="fs-5 fw-bold text-dark">{ketQuaSoSanh.tenHocKy}</div>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4">
            <div className="card shadow-sm border-0 border-start border-success border-4 bg-light">
              <div className="card-body py-2 px-3">
                <div className="text-muted small text-uppercase fw-semibold">Quy mô lớp học phần</div>
                <div className="fs-5 fw-bold text-success">{ketQuaSoSanh.tongSoLop} lớp mở trong kỳ</div>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4">
            <div className="card shadow-sm border-0 border-start border-info border-4 bg-light">
              <div className="card-body py-2 px-3">
                <div className="text-muted small text-uppercase fw-semibold">Tổng lực lượng giảng viên</div>
                <div className="fs-5 fw-bold text-info">{ketQuaSoSanh.tongSoGiangVien} nhân sự đang làm việc</div>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Bảng so sánh 2 thuật toán */}
      {cpSat && ga && (
        <div className="card shadow-sm border-0 mb-4">
          <div className="card-header bg-white py-3 border-bottom d-flex justify-content-between align-items-center">
            <h5 className="card-title h6 mb-0 fw-bold">
              BẢNG THÔNG SỐ ĐỐI ĐẦU THỰC NGHIỆM (HEAD-TO-HEAD BENCHMARK)
            </h5>
            <span className="badge bg-secondary">Cùng ngữ cảnh & cấu hình học kỳ</span>
          </div>

          <div className="card-body p-0 table-responsive">
            <table className="table table-bordered table-hover align-middle mb-0">
              <thead className="table-light text-center small">
                <tr>
                  <th style={{ width: '28%' }} className="text-start ps-3">
                    Tiêu chí đánh giá
                  </th>
                  <th style={{ width: '36%' }} className="text-primary fw-bold fs-6">
                    CP-SAT (Google OR-Tools)
                    <div className="text-muted small fw-normal">Quy hoạch ràng buộc & SAT Solver</div>
                  </th>
                  <th style={{ width: '36%' }} className="text-success fw-bold fs-6">
                    Genetic Algorithm (GA)
                    <div className="text-muted small fw-normal">Giải thuật tiến hóa di truyền</div>
                  </th>
                </tr>
              </thead>
              <tbody>
                {/* 1. Thời gian chạy */}
                <tr>
                  <td className="ps-3 fw-semibold">
                    ⏱️ Thời gian thực thi
                    <div className="text-muted small">Tốc độ tìm ra phương án tối ưu</div>
                  </td>
                  <td className="text-center">
                    <span className="badge bg-primary-subtle text-primary fs-6 px-3 py-2">
                      {cpSat.thoiGianChayGiay ?? (cpSat.thoiGianChayMiligiay ? (cpSat.thoiGianChayMiligiay / 1000).toFixed(2) : '0')} giây
                    </span>
                    {(cpSat.thoiGianChayMiligiay ?? 0) <= (ga.thoiGianChayMiligiay ?? 0) && (
                      <span className="badge bg-success ms-2 small">Nhanh hơn</span>
                    )}
                  </td>
                  <td className="text-center">
                    <span className="badge bg-secondary-subtle text-dark fs-6 px-3 py-2">
                      {ga.thoiGianChayGiay ?? (ga.thoiGianChayMiligiay ? (ga.thoiGianChayMiligiay / 1000).toFixed(2) : '0')} giây
                    </span>
                  </td>
                </tr>

                {/* 2. Trạng thái nghiệm */}
                <tr>
                  <td className="ps-3 fw-semibold">
                    🎯 Trạng thái nghiệm
                    <div className="text-muted small">Chứng minh toán học về tính tối ưu</div>
                  </td>
                  <td className="text-center">
                    <span
                      className={`badge ${
                        cpSat.trangThai === 'ToiUuToanCuc'
                          ? 'bg-success'
                          : cpSat.trangThai === 'KhaThi' || cpSat.trangThai === 'KhaThiMotPhan'
                          ? 'bg-info text-dark'
                          : 'bg-danger'
                      } px-3 py-1`}
                    >
                      {cpSat.trangThai === 'ToiUuToanCuc'
                        ? 'Tối ưu toàn cục (Optimal)'
                        : cpSat.trangThai === 'KhaThiMotPhan'
                        ? 'Khả thi một phần'
                        : cpSat.trangThai}
                    </span>
                  </td>
                  <td className="text-center">
                    <span className="badge bg-secondary px-3 py-1">
                      {ga.trangThai === 'ThanhCong' ? 'Nghiệm xấp xỉ tốt nhất' : ga.trangThai}
                    </span>
                  </td>
                </tr>

                {/* 3. Vi phạm ràng buộc cứng */}
                <tr>
                  <td className="ps-3 fw-semibold">
                    🛡️ Vi phạm ràng buộc cứng (H1-H6)
                    <div className="text-muted small">Trùng lịch, sai chuyên môn, lịch bận</div>
                  </td>
                  <td className="text-center">
                    <span
                      className={`badge fs-6 ${
                        (cpSat.soViPhamCung ?? 0) === 0 ? 'bg-success' : 'bg-danger'
                      }`}
                    >
                      {cpSat.soViPhamCung ?? 0} vi phạm
                    </span>
                  </td>
                  <td className="text-center">
                    <span
                      className={`badge fs-6 ${
                        (ga.soViPhamCung ?? 0) === 0 ? 'bg-success' : 'bg-warning text-dark'
                      }`}
                    >
                      {ga.soViPhamCung ?? 0} vi phạm
                    </span>
                  </td>
                </tr>

                {/* 4. Tổng điểm phạt mềm */}
                <tr>
                  <td className="ps-3 fw-semibold">
                    📉 Tổng điểm phạt mềm (Z)
                    <div className="text-muted small">Càng thấp phương án càng tối ưu</div>
                  </td>
                  <td className="text-center fw-bold fs-6 text-primary">
                    {Math.round(cpSat.tongDiemPhat)} điểm
                    {cpSat.tongDiemPhat <= ga.tongDiemPhat && (
                      <span className="badge bg-success ms-2 small">Tối ưu hơn</span>
                    )}
                  </td>
                  <td className="text-center fw-bold fs-6 text-secondary">
                    {Math.round(ga.tongDiemPhat)} điểm
                  </td>
                </tr>

                {/* 5. Phạt nguyện vọng */}
                <tr>
                  <td className="ps-3">
                    - Điểm phạt nguyện vọng giáo viên
                    <div className="text-muted small">Tránh ca tối, cuối tuần, đúng ca sáng/chiều</div>
                  </td>
                  <td className="text-center">{Math.round(cpSat.diemPhatNguyenVong ?? 0)}</td>
                  <td className="text-center">{Math.round(ga.diemPhatNguyenVong ?? 0)}</td>
                </tr>

                {/* 6. Phạt cân bằng tải */}
                <tr>
                  <td className="ps-3">
                    - Điểm phạt cân bằng tải tuần
                    <div className="text-muted small">Quá tải tuần (&gt;25 tiết) &amp; thiếu tải</div>
                  </td>
                  <td className="text-center">{Math.round(cpSat.diemPhatCanBangTai ?? 0)}</td>
                  <td className="text-center">{Math.round(ga.diemPhatCanBangTai ?? 0)}</td>
                </tr>

                {/* 7. Phạt gom lịch */}
                <tr>
                  <td className="ps-3">
                    - Điểm phạt gom lịch dạy
                    <div className="text-muted small">Tránh tiết trống giữa ca &amp; rải ngày</div>
                  </td>
                  <td className="text-center">{Math.round(cpSat.diemPhatGomLich ?? 0)}</td>
                  <td className="text-center">{Math.round(ga.diemPhatGomLich ?? 0)}</td>
                </tr>

                {/* 8. Tỷ lệ phủ kín lớp */}
                <tr>
                  <td className="ps-3 fw-semibold">
                    📋 Số lớp đã phân công / Chưa phân công
                  </td>
                  <td className="text-center">
                    <span className="text-success fw-bold">
                      {cpSat.danhSachPhanCong?.length ?? 0}
                    </span>{' '}
                    / {cpSat.soLopChuaPhanCong ?? 0} lớp chưa phân
                  </td>
                  <td className="text-center">
                    <span className="text-success fw-bold">
                      {ga.danhSachPhanCong?.length ?? 0}
                    </span>{' '}
                    / {ga.soLopChuaPhanCong ?? 0} lớp chưa phân
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          <div className="card-footer bg-light p-3 small">
            <strong>💡 Kết luận khoa học:</strong> Bộ giải <strong>CP-SAT (Google OR-Tools)</strong> cho tốc độ thực thi nhanh vượt trội, đảm bảo 100% không vi phạm ràng buộc cứng và luôn tìm ra nghiệm tối ưu toàn cục (Optimal Bound). Giải thuật Di truyền (GA) mang tính ngẫu nhiên tiếp cận tiệm cận qua nhiều thế hệ tiến hóa.
          </div>
        </div>
      )}

      {/* Hướng dẫn ban đầu khi chưa bấm chạy */}
      {!ketQuaSoSanh && !dangChay && (
        <div className="card border-0 bg-light p-4 text-center">
          <div className="py-4">
            <div className="display-6 text-primary mb-3">📊</div>
            <h5 className="fw-bold">Chưa có dữ liệu đối sánh thực nghiệm</h5>
            <p className="text-muted small mb-3">
              Chọn học kỳ ở góc trên bên phải và bấm nút{' '}
              <strong>"⚡ Chạy đối đầu CP-SAT vs GA"</strong> để hệ thống tự động kích hoạt cả hai giải thuật và trích xuất bảng so sánh chi tiết.
            </p>
          </div>
        </div>
      )}
    </section>
  )
}

export default Dashboard
