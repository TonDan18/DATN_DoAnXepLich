import Dashboard from './pages/Dashboard/Dashboard'
import { createBrowserRouter, RouterProvider, Navigate } from 'react-router-dom'
import { AuthProvider, useAuth } from './contexts/AuthContext'
import MainLayout from './layouts/MainLayout'
import DanhSachGiangVien from './pages/GiangVien/DanhSachGiangVien'
import DanhSachHocKy from './pages/HocKy/DanhSachHocKy'
import DanhSachHocPhan from './pages/HocPhan/DanhSachHocPhan'
import KetQuaXepLich from './pages/KetQua/KetQuaXepLich'
import DanhSachNamHoc from './pages/NamHoc/DanhSachNamHoc'
import DanhSachNguyenVong from './pages/NguyenVong/DanhSachNguyenVong'
import DanhSachRangBuoc from './pages/RangBuoc/DanhSachRangBuoc'
import DanhSachLopHocPhan from './pages/ThoiKhoaBieu/DanhSachLopHocPhan'
import ChayXepLich from './pages/XepLich/ChayXepLich'
import Login from './pages/Auth/Login'
import DanhSachNguoiDung from './pages/NguoiDung/DanhSachNguoiDung'
import DanhSachBoMon from './pages/BoMon/DanhSachBoMon'

function ProtectedLayout() {
  const { user, dangTai } = useAuth()

  if (dangTai) {
    return <div className="p-4 text-center text-muted">Đang tải thông tin...</div>
  }

  if (!user) {
    return <Navigate to="/login" replace />
  }

  return <MainLayout />
}

function AdminOnlyRoute({ children }: { children: React.ReactNode }) {
  const { user } = useAuth()
  if (user?.vaiTro !== 'Admin') {
    return <Navigate to="/" replace />
  }
  return <>{children}</>
}

function TrangChuRedirect() {
  const { user } = useAuth()
  if (user?.vaiTro === 'Admin') {
    return <Navigate to="/tai-khoan" replace />
  }
  if (user?.vaiTro === 'TruongBoMon') {
    return <Navigate to="/dashboard" replace />
  }
  return <Navigate to="/thoi-khoa-bieu" replace />
}

const router = createBrowserRouter([
  {
    path: '/login',
    element: <Login />,
  },
  {
    path: '/',
    element: <ProtectedLayout />,
    children: [
      { index: true, element: <TrangChuRedirect /> },
      { path: 'dashboard', element: <Dashboard /> },
      { path: 'nam-hoc', element: <DanhSachNamHoc /> },
      { path: 'hoc-ky', element: <DanhSachHocKy /> },
      { path: 'bo-mon', element: <DanhSachBoMon /> },
      { path: 'giang-vien', element: <DanhSachGiangVien /> },
      { path: 'hoc-phan', element: <DanhSachHocPhan /> },
      { path: 'thoi-khoa-bieu', element: <DanhSachLopHocPhan /> },
      { path: 'nguyen-vong', element: <DanhSachNguyenVong /> },
      { path: 'rang-buoc', element: <DanhSachRangBuoc /> },
      { path: 'xep-lich', element: <ChayXepLich /> },
      { path: 'ket-qua', element: <KetQuaXepLich /> },
      {
        path: 'tai-khoan',
        element: (
          <AdminOnlyRoute>
            <DanhSachNguoiDung />
          </AdminOnlyRoute>
        ),
      },
    ],
  },
])

function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}

export default App
