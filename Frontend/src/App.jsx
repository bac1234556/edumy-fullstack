import { BrowserRouter as Router, Routes, Route, useLocation, useNavigate } from 'react-router-dom';
import { useContext, useEffect } from 'react';
import { AuthContext } from './context/AuthContext';
import HomePage from './pages/HomePage';
import CourseList from './pages/CourseList';
import CourseDetail from './pages/CourseDetail';
import Login from './pages/Login';
import Register from './pages/Register';
import ForgotPassword from './pages/ForgotPassword';
import ResetPassword from './pages/ResetPassword';
import LoginSuccess from './pages/LoginSuccess';
import InstructorDashboard from './pages/InstructorDashboard';
import CourseCreate from './pages/CourseCreate';
import CourseEdit from './pages/CourseEdit';
import Cart from './pages/Cart';
import MyLearning from './pages/MyLearning';
import CoursePlayer from './pages/CoursePlayer';
import AdminDashboard from './pages/AdminDashboard';
import PaymentSuccess from './pages/PaymentSuccess';
import PaymentCancel from './pages/PaymentCancel';
import MockPaymentGateway from './pages/MockPaymentGateway';
import CertificateView from './pages/CertificateView';
import Wishlist from './pages/Wishlist';
import UserProfile from './pages/UserProfile';
import TeachOnEdumy from './pages/TeachOnEdumy';
import AdminInstructorApplications from './pages/AdminInstructorApplications';
import PublicProfile from './pages/PublicProfile';
import AboutUs from './pages/AboutUs';
import Blog from './pages/Blog';
import HelpSupport from './pages/HelpSupport';
import TermsPrivacy from './pages/TermsPrivacy';
import ProtectedRoute from './components/ProtectedRoute';
import Navbar from './components/Navbar';
import Footer from './components/Footer';
import ScrollToTop from './components/ScrollToTop';
import { Toaster } from 'react-hot-toast';

function AuthEventHandler() {
  const navigate = useNavigate();
  const location = useLocation();
  const { clearSession } = useContext(AuthContext);
  useEffect(() => {
    const redirectToLogin = event => {
      clearSession();
      if (!location.pathname.startsWith('/login')) {
        const returnUrl = `${location.pathname}${location.search}`;
        navigate(`/login?returnUrl=${encodeURIComponent(returnUrl)}`, { replace: true, state: { flash: { type: 'error', message: event.detail?.message || 'Bạn cần đăng nhập lại.' } } });
      }
    };
    const inactive = event => {
      clearSession();
      const email = event.detail?.adminEmail ? ` Liên hệ ${event.detail.adminEmail}.` : '';
      const message = `${event.detail?.message || 'Tài khoản của bạn đã bị khóa.'}${email}`;
      if (!location.pathname.startsWith('/login')) navigate('/login?inactive=1', { replace: true, state: { flash: { type: 'error', message } } });
    };
    window.addEventListener('edumy:auth-required', redirectToLogin);
    window.addEventListener('edumy:account-inactive', inactive);
    return () => {
      window.removeEventListener('edumy:auth-required', redirectToLogin);
      window.removeEventListener('edumy:account-inactive', inactive);
    };
  }, [clearSession, location.pathname, location.search, navigate]);
  return null;
}

function App() {
  return (
    <Router>
      <div className="App">
        <AuthEventHandler />
        <ScrollToTop />
        <Toaster position="bottom-right" toastOptions={{ duration: 3500, style: { maxWidth: '400px', wordBreak: 'break-word' } }} />
        <Navbar />

        <main className="main-content">
          <Routes>
            <Route path="/" element={<HomePage />} />
            <Route path="/courses" element={<CourseList />} />
            <Route path="/courses/:id" element={<CourseDetail />} />
            <Route path="/login" element={<Login />} />
            <Route path="/register" element={<Register />} />
            <Route path="/forgot-password" element={<ForgotPassword />} />
            <Route path="/reset-password" element={<ResetPassword />} />
            <Route path="/login-success" element={<LoginSuccess />} />
            
            {/* Student Routes */}
            <Route path="/cart" element={
              <ProtectedRoute allowedRoles={['Student']}>
                <Cart />
              </ProtectedRoute>
            } />
            <Route path="/my-courses" element={
              <ProtectedRoute allowedRoles={['Student']}>
                <MyLearning />
              </ProtectedRoute>
            } />
            <Route path="/teach-on-edumy" element={
              <ProtectedRoute allowedRoles={['Student']}>
                <TeachOnEdumy />
              </ProtectedRoute>
            } />
            <Route path="/my-courses/:id/learn" element={
              <ProtectedRoute allowedRoles={['Student']}>
                <CoursePlayer />
              </ProtectedRoute>
            } />
            <Route path="/wishlist" element={
              <ProtectedRoute allowedRoles={['Student']}>
                <Wishlist />
              </ProtectedRoute>
            } />
            <Route path="/payment" element={
              <ProtectedRoute allowedRoles={['Student']}>
                <MockPaymentGateway />
              </ProtectedRoute>
            } />
            <Route path="/payment-success" element={<PaymentSuccess />} />
            <Route path="/payment-cancel" element={<PaymentCancel />} />
            
            {/* Common Authenticated Routes */}
            <Route path="/profile" element={
              <ProtectedRoute allowedRoles={['Student', 'Instructor', 'Admin']}>
                <UserProfile />
              </ProtectedRoute>
            } />
            <Route path="/users/:id" element={<PublicProfile />} />

            {/* Instructor Routes */}
            <Route path="/instructor" element={
              <ProtectedRoute allowedRoles={['Instructor']}>
                <InstructorDashboard />
              </ProtectedRoute>
            } />
            <Route path="/instructor/courses/new" element={
              <ProtectedRoute allowedRoles={['Instructor']}>
                <CourseCreate />
              </ProtectedRoute>
            } />
            <Route path="/instructor/courses/:id/edit" element={
              <ProtectedRoute allowedRoles={['Instructor']}>
                <CourseEdit />
              </ProtectedRoute>
            } />
            <Route path="/instructor/courses/:id/preview" element={
              <ProtectedRoute allowedRoles={['Instructor']}>
                <CoursePlayer />
              </ProtectedRoute>
            } />
            <Route path="/instructor/courses/:id/discussions" element={
              <ProtectedRoute allowedRoles={['Instructor']}>
                <CoursePlayer />
              </ProtectedRoute>
            } />

            {/* Admin Routes */}
            <Route path="/admin" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <AdminDashboard />
              </ProtectedRoute>
            } />
            <Route path="/admin/courses/:id/preview" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <CoursePlayer />
              </ProtectedRoute>
            } />
            <Route path="/admin/instructor-applications" element={
              <ProtectedRoute allowedRoles={['Admin']}>
                <AdminInstructorApplications />
              </ProtectedRoute>
            } />

            <Route path="/certificates/:url" element={<CertificateView />} />
            <Route path="/about" element={<AboutUs />} />
            <Route path="/blog" element={<Blog />} />
            <Route path="/help" element={<HelpSupport />} />
            <Route path="/terms" element={<TermsPrivacy />} />
          </Routes>
        </main>
        
        <Footer />
      </div>
    </Router>
  );
}

export default App;
