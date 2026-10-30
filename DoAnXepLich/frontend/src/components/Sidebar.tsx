import { NavLink } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

const adminMenuItems = [
  { path: '/tai-khoan', label: 'Quản lý tài khoản' },
  { path: '/nam-hoc', label: 'Quản lý năm học' },
  { path: '/hoc-ky', label: 'Quản lý học kỳ' },
  { path: '/bo-mon', label: 'Quản lý bộ môn' },
  { path: '/giang-vien', label: 'Quản lý giảng viên' },
  { path: '/hoc-phan', label: 'Quản lý học phần' },
  { path: '/thoi-khoa-bieu', label: 'Thời khóa biểu' },
  { path: '/rang-buoc', label: 'Cấu hình ràng buộc' },
  { path: '/xep-lich', label: 'Xếp lịch tự động' },
  { path: '/ket-qua', label: 'Kết quả xếp lịch' },
]

const truongBoMonMenuItems = [
  { path: '/dashboard', label: '📊 Tổng quan & So sánh' },
  { path: '/giang-vien', label: 'Quản lý giảng viên' },
  { path: '/hoc-phan', label: 'Quản lý học phần' },
  { path: '/thoi-khoa-bieu', label: 'Thời khóa biểu' },
  { path: '/nguyen-vong', label: 'Lịch bận & Nguyện vọng' },
  { path: '/rang-buoc', label: 'Cấu hình ràng buộc' },
  { path: '/xep-lich', label: 'Xếp lịch tự động' },
  { path: '/ket-qua', label: 'Kết quả xếp lịch' },
]

const giangVienMenuItems = [
  { path: '/thoi-khoa-bieu', label: 'Thời khóa biểu' },
  { path: '/nguyen-vong', label: 'Nguyện vọng' },
  { path: '/ket-qua', label: 'Kết quả xếp lịch' },
]

function Sidebar() {
  const { user } = useAuth()
  let items = giangVienMenuItems
  if (user?.vaiTro === 'Admin') {
    items = adminMenuItems
  } else if (user?.vaiTro === 'TruongBoMon') {
    items = truongBoMonMenuItems
  }

  return (
    <aside className="sidebar border-end bg-light p-3">
      <div className="fw-semibold mb-3">DoAnXepLich</div>
      <nav className="nav nav-pills flex-column gap-1">
        {items.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `nav-link ${isActive ? 'active' : 'text-dark'}`
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  )
}

export default Sidebar
