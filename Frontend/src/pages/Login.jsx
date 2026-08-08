import { useContext, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AuthContext } from '../context/AuthContext';
import './Auth.css';

function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const { login } = useContext(AuthContext);
  const navigate = useNavigate();
  const location = useLocation();

  useEffect(() => {
    if (location.state?.flash) {
      const flash = location.state.flash;
      if (flash.type === 'success') setNotice(flash.message); else setError(flash.message);
      navigate(`${location.pathname}${location.search}`, { replace: true, state: null });
      return;
    }
    const raw = sessionStorage.getItem('accountInactive');
    if (!raw) return;
    sessionStorage.removeItem('accountInactive');
    try {
      const value = JSON.parse(raw);
      setError(`${value.message}${value.adminEmail ? ` Liên hệ: ${value.adminEmail}` : ''}`);
    } catch { setError('Tài khoản của bạn đã bị khóa.'); }
  }, [location.pathname, location.search, location.state, navigate]);

  const handleLogin = async event => {
    event.preventDefault();
    try {
      setError(''); setNotice('');
      await login(email, password);
      // All user roles always navigate directly to Edumy Home '/' after successful login
      navigate('/', { replace: true });
    } catch (requestError) {
      if (requestError.response?.data?.code === 'ACCOUNT_INACTIVE') sessionStorage.removeItem('accountInactive');
      setError(requestError.response?.data?.message || 'Đăng nhập thất bại. Vui lòng thử lại.');
    }
  };

  return <div className="auth-container"><div className="auth-card hover-3d">
    <h2 className="auth-title">Log in to your EduMy account</h2>
    {error && <div role="alert" style={{ color: '#b32d0f', backgroundColor: '#fcd3ce', padding: 12, marginBottom: 16, borderRadius: 4, fontSize: 14, fontWeight: 'bold' }}>{error}</div>}
    {notice && <div role="status" style={{ color: '#146c43', backgroundColor: '#d1e7dd', padding: 12, marginBottom: 16, borderRadius: 4, fontSize: 14, fontWeight: 'bold' }}>{notice}</div>}
    <form className="auth-form" onSubmit={handleLogin}>
      <div className="form-group"><input type="email" value={email} onChange={event => setEmail(event.target.value)} placeholder="Email" autoComplete="email" required /></div>
      <div className="form-group"><input type="password" value={password} onChange={event => setPassword(event.target.value)} placeholder="Password" autoComplete="current-password" required /></div>
      <button type="submit" className="auth-submit-btn">Log in</button>
    </form>
    <div className="auth-footer-text">Don't have an account? <Link to="/register">Sign up</Link></div>
  </div></div>;
}

export default Login;
