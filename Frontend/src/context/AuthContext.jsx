import { createContext, useCallback, useEffect, useState } from 'react';
import api, { resetAuthFailureState } from '../api/axiosConfig';
import { normalizeUser, normalizeRole, USER_ROLES } from '../utils/userUtils';

export const AuthContext = createContext();

const parseToken = token => {
  if (typeof token !== 'string' || !token.trim()) return null;
  try {
    const value = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const payload = JSON.parse(atob(value));
    const rawRole = payload.role || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || '';
    const raw = {
      id: payload.sub || payload.nameid || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
      email: payload.email || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'],
      fullName: payload.unique_name || payload.name || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || 'User',
      role: rawRole,
      avatarUrl: payload.avatarUrl || payload.avatar
    };
    return normalizeUser(raw);
  } catch { return null; }
};

const clearPrivateCache = () => {
  Object.keys(localStorage).forEach(key => {
    if (key === 'token' || key === 'lastReadOrdersCount' || key.startsWith('edumy:')) localStorage.removeItem(key);
  });
};

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  const clearSession = useCallback(() => { clearPrivateCache(); setUser(null); }, []);

  const refreshUserProfile = useCallback(async () => {
    try {
      const response = await api.get('/account/me');
      const normalized = normalizeUser(response.data);
      if (normalized) {
        setUser(prev => {
          if (!prev) return normalized;
          return {
            ...prev,
            ...normalized,
            role: normalized.role || prev.role
          };
        });
      }
    } catch {
      // quiet fallback
    }
  }, []);

  const acceptToken = useCallback(token => {
    localStorage.setItem('token', token);
    resetAuthFailureState();
    const decoded = parseToken(token);
    setUser(decoded);
    if (decoded) {
      refreshUserProfile();
    }
  }, [refreshUserProfile]);

  const updateUserProfile = useCallback(updatedData => {
    const normalized = normalizeUser(updatedData);
    if (normalized) {
      setUser(prev => {
        if (!prev) return normalized;
        return {
          ...prev,
          ...normalized,
          role: normalized.role || prev.role
        };
      });
    }
  }, []);

  useEffect(() => {
    const token = localStorage.getItem('token');
    const decoded = token ? parseToken(token) : null;
    if (decoded) {
      setUser(decoded);
      refreshUserProfile();
    } else if (token) {
      localStorage.removeItem('token');
    }
    setLoading(false);
  }, [refreshUserProfile]);

  useEffect(() => {
    const clear = () => clearSession();
    const refreshed = event => {
      if (event.detail?.token) {
        const decoded = parseToken(event.detail.token);
        setUser(decoded);
        if (decoded) refreshUserProfile();
      }
    };
    window.addEventListener('edumy:auth-required', clear);
    window.addEventListener('edumy:account-inactive', clear);
    window.addEventListener('edumy:token-refreshed', refreshed);
    return () => {
      window.removeEventListener('edumy:auth-required', clear);
      window.removeEventListener('edumy:account-inactive', clear);
      window.removeEventListener('edumy:token-refreshed', refreshed);
    };
  }, [clearSession, refreshUserProfile]);

  const login = async (email, password) => {
    const response = await api.post('/auth/login', { email, password });
    acceptToken(response.data.token);
    return response.data.message;
  };
  const googleLogin = async googleToken => {
    const response = await api.post('/auth/google-login', { token: googleToken });
    acceptToken(response.data.token);
    return response.data.message;
  };
  const logout = useCallback(async () => {
    try { await api.post('/auth/revoke-token'); } catch { /* Local logout must always complete. */ }
    clearSession();
  }, [clearSession]);

  const currentRole = normalizeRole(user?.role);
  const isStudent = currentRole === USER_ROLES.STUDENT;
  const isInstructor = currentRole === USER_ROLES.INSTRUCTOR;
  const isAdmin = currentRole === USER_ROLES.ADMIN;

  return <AuthContext.Provider value={{
    user, loading, login, googleLogin, logout, clearSession, loginWithToken: acceptToken, updateUserProfile, refreshUserProfile,
    isStudent, isInstructor, isAdmin
  }}>{children}</AuthContext.Provider>;
};
