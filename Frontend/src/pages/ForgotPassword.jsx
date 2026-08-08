import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import './Auth.css';

const ForgotPassword = () => {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [devToken, setDevToken] = useState('');
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setMessage('');
    setError('');
    setDevToken('');

    try {
      const response = await api.post('/auth/forgot-password', { email });
      setMessage(response.data.message || 'Token generated successfully.');
      if (response.data.resetToken) {
        setDevToken(response.data.resetToken);
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Something went wrong. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-card">
        <h2>Forgot Password</h2>
        <p className="auth-subtitle">Enter your email address and we'll generate a reset token for you.</p>
        
        {message && <div className="alert alert-success">{message}</div>}
        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label htmlFor="email">Email address</label>
            <input
              type="email"
              id="email"
              placeholder="e.g. email@example.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>

          <button type="submit" className="btn-edumy-primary w-100" disabled={loading}>
            {loading ? 'Processing...' : 'Send Reset Code'}
          </button>
        </form>

        {devToken && (
          <div className="dev-token-box mt-3 p-3 bg-light border rounded">
            <h6 className="text-warning fw-bold">🔧 Dev Testing Mode:</h6>
            <p className="small mb-2">Since email server is mocked, here is your reset token:</p>
            <div className="d-flex justify-content-between align-items-center bg-white p-2 border rounded">
              <code className="fw-bold">{devToken}</code>
              <button 
                onClick={() => navigate(`/reset-password?email=${encodeURIComponent(email)}&token=${devToken}`)}
                className="btn btn-sm btn-outline-primary"
              >
                Go to Reset Form
              </button>
            </div>
          </div>
        )}

        <div className="auth-footer">
          Remember your password? <Link to="/login">Log in</Link>
        </div>
      </div>
    </div>
  );
};

export default ForgotPassword;
