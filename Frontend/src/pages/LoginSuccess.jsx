import { useEffect, useContext } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { AuthContext } from '../context/AuthContext';

function LoginSuccess() {
  const [searchParams] = useSearchParams();
  const { loginWithToken } = useContext(AuthContext);
  const navigate = useNavigate();

  useEffect(() => {
    const token = searchParams.get('token');
    if (token) {
      try {
        loginWithToken(token);
        // All user roles always navigate directly to Edumy Home '/' after Google login
        navigate('/', { replace: true });
      } catch (err) {
        console.error('Failed to authenticate Google user:', err);
        navigate('/login', { replace: true });
      }
    } else {
      navigate('/login', { replace: true });
    }
  }, [searchParams, loginWithToken, navigate]);

  return (
    <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '60vh', flexDirection: 'column', gap: '15px' }}>
      <div className="spinner-border text-primary" role="status" style={{ width: '3rem', height: '3rem' }}>
        <span className="visually-hidden">Loading...</span>
      </div>
      <h3 style={{ color: '#1e293b', fontWeight: 'bold' }}>Authenticating with Google...</h3>
      <p style={{ color: '#64748b' }}>Please wait while we set up your session.</p>
    </div>
  );
}

export default LoginSuccess;
