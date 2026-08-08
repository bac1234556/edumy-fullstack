import React from 'react';
import { AlertTriangle, RefreshCw, Home } from 'lucide-react';

class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error("ErrorBoundary caught an error:", error, errorInfo);
    this.setState({ errorInfo });
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
    window.location.href = '/';
  };

  handleReload = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return (
        <div style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          minHeight: '100vh',
          backgroundColor: '#f8fafc',
          padding: '24px',
          fontFamily: 'system-ui, -apple-system, sans-serif',
          color: '#1e293b',
          textAlign: 'center'
        }}>
          <div style={{
            maxWidth: '560px',
            backgroundColor: '#ffffff',
            borderRadius: '16px',
            padding: '40px',
            boxShadow: '0 20px 25px -5px rgba(109, 93, 252, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.02)',
            border: '1px solid #e2e8f0',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center'
          }}>
            <div style={{
              width: '64px',
              height: '64px',
              borderRadius: '50%',
              backgroundColor: 'rgba(239, 68, 68, 0.1)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              marginBottom: '24px'
            }}>
              <AlertTriangle size={36} color="#ef4444" />
            </div>

            <h1 style={{
              fontSize: '24px',
              fontWeight: '800',
              marginBottom: '12px',
              color: '#0f172a'
            }}>
              Oops! Something went wrong
            </h1>

            <p style={{
              fontSize: '15px',
              color: '#64748b',
              lineHeight: '1.6',
              marginBottom: '24px'
            }}>
              We encountered an unexpected error. This might be due to a lost connection to the configured Backend API service or a rendering error.
            </p>

            <div style={{
              backgroundColor: '#f1f5f9',
              borderRadius: '8px',
              padding: '16px',
              width: '100%',
              textAlign: 'left',
              marginBottom: '32px',
              border: '1px solid #cbd5e1'
            }}>
              <div style={{ fontWeight: '700', fontSize: '12px', color: '#475569', textTransform: 'uppercase', marginBottom: '8px' }}>
                Error Details
              </div>
              <code style={{
                fontSize: '13px',
                color: '#ef4444',
                wordBreak: 'break-all',
                whiteSpace: 'pre-wrap',
                fontFamily: 'monospace'
              }}>
                {this.state.error?.toString() || 'Unknown connection or rendering error.'}
              </code>
            </div>

            <div style={{
              display: 'flex',
              gap: '12px',
              width: '100%',
              justifyContent: 'center'
            }}>
              <button
                onClick={this.handleReload}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: '#6d5dfc',
                  color: '#ffffff',
                  border: 'none',
                  padding: '12px 20px',
                  borderRadius: '8px',
                  fontSize: '14px',
                  fontWeight: '600',
                  cursor: 'pointer',
                  transition: 'background-color 0.2s',
                  boxShadow: '0 4px 6px -1px rgba(109, 93, 252, 0.2)'
                }}
                onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#5848e5'}
                onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#6d5dfc'}
              >
                <RefreshCw size={16} /> Reload Page
              </button>

              <button
                onClick={this.handleReset}
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  backgroundColor: '#ffffff',
                  color: '#0f172a',
                  border: '1px solid #cbd5e1',
                  padding: '12px 20px',
                  borderRadius: '8px',
                  fontSize: '14px',
                  fontWeight: '600',
                  cursor: 'pointer',
                  transition: 'background-color 0.2s'
                }}
                onMouseOver={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                onMouseOut={(e) => e.currentTarget.style.backgroundColor = '#ffffff'}
              >
                <Home size={16} /> Back to Home
              </button>
            </div>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
