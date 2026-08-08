import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import 'bootstrap/dist/css/bootstrap.min.css';
import './index.css'
import App from './App.jsx'
import { AuthProvider } from './context/AuthContext'
import { GoogleOAuthProvider } from '@react-oauth/google';

import ErrorBoundary from './components/ErrorBoundary.jsx';
import { CategoryProvider } from './context/CategoryContext.jsx';

const clientId = 'YOUR_GOOGLE_CLIENT_ID_HERE'; // Replace with real client ID

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <ErrorBoundary>
      <GoogleOAuthProvider clientId={clientId}>
        <AuthProvider>
          <CategoryProvider><App /></CategoryProvider>
        </AuthProvider>
      </GoogleOAuthProvider>
    </ErrorBoundary>
  </StrictMode>,
)
