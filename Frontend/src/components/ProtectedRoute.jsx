import { useContext } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { AuthContext } from '../context/AuthContext';
import { normalizeRole } from '../utils/userUtils';

function ProtectedRoute({ children, allowedRoles }) {
  const { user, loading } = useContext(AuthContext);
  const location = useLocation();

  if (loading) {
    return <div className="p-4 text-center">Loading...</div>;
  }

  if (!user) {
    const returnUrl = `${location.pathname}${location.search}`;
    return <Navigate to={`/login?returnUrl=${encodeURIComponent(returnUrl)}`} replace state={{ flash: { type: 'error', message: 'Vui lòng đăng nhập để tiếp tục.' } }} />;
  }

  const userRole = normalizeRole(user.role);
  const isAllowed = allowedRoles ? allowedRoles.some(r => normalizeRole(r) === userRole) : true;

  if (allowedRoles && !isAllowed) {
    // Silently redirect forbidden roles to Edumy Home '/' without role toasts
    return <Navigate to="/" replace />;
  }

  return children;
}

export default ProtectedRoute;
