import axios from 'axios';
import { resolveTenantCode } from '../lib/tenant';

/** Axios dùng chung: gắn tenant (X-Tenant) và token đăng nhập vào mọi request. */
export const http = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '',
  timeout: 15000,
});

http.interceptors.request.use((config) => {
  const tenant = resolveTenantCode(window.location.hostname, import.meta.env.VITE_DEV_TENANT);
  if (tenant) {
    config.headers.set('X-Tenant', tenant);
  }
  const token = localStorage.getItem('contrack.accessToken');
  if (token) {
    config.headers.set('Authorization', `Bearer ${token}`);
  }
  return config;
});
