import axios from 'axios';

const baseURL = import.meta.env.VITE_API_URL || '/api';
const api = axios.create({ baseURL, withCredentials: true, headers: { 'Content-Type': 'application/json' } });
const refreshClient = axios.create({ baseURL, withCredentials: true, headers: { 'Content-Type': 'application/json' } });
let refreshPromise = null;
let authFailureNotified = false;

export function resetAuthFailureState() { authFailureNotified = false; }

function emitInactive(payload) {
  localStorage.removeItem('token');
  sessionStorage.setItem('accountInactive', JSON.stringify({ message: payload?.message, adminEmail: payload?.adminEmail }));
  window.dispatchEvent(new CustomEvent('edumy:account-inactive', { detail: payload }));
}

function emitAuthRequired() {
  localStorage.removeItem('token');
  if (authFailureNotified) return;
  authFailureNotified = true;
  window.dispatchEvent(new CustomEvent('edumy:auth-required', { detail: { message: 'Phiên đăng nhập đã hết hạn.' } }));
}

api.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(response => response, async error => {
  const payload = error.response?.data;
  if (error.response?.status === 403 && payload?.code === 'ACCOUNT_INACTIVE') {
    emitInactive(payload);
    return Promise.reject(error);
  }

  const original = error.config || {};
  const path = String(original.url || '');
  const authEndpoint = /\/auth\/(login|google-login|refresh-token|revoke-token)/.test(path);
  if (error.response?.status !== 401 || original._retry || authEndpoint || !localStorage.getItem('token')) {
    return Promise.reject(error);
  }

  original._retry = true;
  if (!refreshPromise) {
    refreshPromise = refreshClient.post('/auth/refresh-token')
      .then(response => {
        const token = response.data?.token;
        if (!token) throw new Error('Refresh response did not contain an access token.');
        localStorage.setItem('token', token);
        resetAuthFailureState();
        window.dispatchEvent(new CustomEvent('edumy:token-refreshed', { detail: { token } }));
        return token;
      })
      .catch(refreshError => {
        const refreshPayload = refreshError.response?.data;
        if (refreshError.response?.status === 403 && refreshPayload?.code === 'ACCOUNT_INACTIVE') emitInactive(refreshPayload);
        else emitAuthRequired();
        throw refreshError;
      })
      .finally(() => { refreshPromise = null; });
  }

  try {
    const token = await refreshPromise;
    original.headers = original.headers || {};
    original.headers.Authorization = `Bearer ${token}`;
    return api(original);
  } catch {
    return Promise.reject(error);
  }
});

export default api;
