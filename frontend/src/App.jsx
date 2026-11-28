import { Navigate, Route, Routes } from 'react-router-dom'
import Layout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import LoginPage from './pages/LoginPage'
import SignupPage from './pages/SignupPage'
import PlaceholderPage from './pages/PlaceholderPage'

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/signup" element={<SignupPage />} />

      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route path="/dashboard" element={<PlaceholderPage title="Dashboard" />} />
          <Route path="/tasks" element={<PlaceholderPage title="Task List" />} />
          <Route path="/tasks/new" element={<PlaceholderPage title="New Task" />} />
          <Route path="/tasks/:id" element={<PlaceholderPage title="Task Detail" />} />
          <Route path="/profile" element={<PlaceholderPage title="User Profile" />} />

          <Route element={<ProtectedRoute adminOnly />}>
            <Route path="/admin/users" element={<PlaceholderPage title="Users (Admin)" />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  )
}