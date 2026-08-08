import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { toast } from 'react-hot-toast';
import './Auth.css';

function Register() {
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const handleRegister = async (e) => {
    e.preventDefault();
    try {
      setError('');
      await api.post('/auth/register', { fullName, email, password });
      toast.success('Đăng ký tài khoản thành công! Vui lòng đăng nhập.');
      navigate('/login');
    } catch (err) {
      const errMsg = err.response?.data?.message || 'Registration failed.';
      setError(errMsg);
      toast.error(errMsg);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-card hover-3d">
        <h2 className="auth-title">Sign up and start learning</h2>

        {error && <div style={{ color: '#b32d0f', backgroundColor: '#fcd3ce', padding: '12px', marginBottom: '16px', borderRadius: '4px', fontSize: '14px', fontWeight: 'bold' }}>{error}</div>}
        
        <form className="auth-form" onSubmit={handleRegister}>
          <div className="form-group">
            <input 
              type="text" 
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              placeholder="Full Name"
              required 
            />
          </div>
          <div className="form-group">
            <input 
              type="email" 
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="Email"
              required 
            />
          </div>
          <div className="form-group">
            <input 
              type="password" 
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Password"
              required 
              minLength="6"
            />
          </div>
          <button type="submit" className="auth-submit-btn">Sign up</button>
        </form>
        
        <div className="auth-footer-text" style={{ fontSize: '12px', marginBottom: '16px' }}>
          By signing up, you agree to our <a href="#">Terms of Use</a> and <a href="#">Privacy Policy</a>.
        </div>
        
        <div className="auth-divider"></div>

        <div className="auth-footer-text">
          Already have an account? <Link to="/login">Log in</Link>
        </div>
      </div>
    </div>
  );
}

export default Register;

