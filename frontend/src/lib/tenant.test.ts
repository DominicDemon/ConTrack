import { describe, expect, it } from 'vitest';
import { resolveTenantCode } from './tenant';

describe('resolveTenantCode', () => {
  it('lấy subdomain làm mã tenant', () => {
    expect(resolveTenantCode('acme.contrack.vn')).toBe('acme');
  });

  it('dùng tenant dev khi chạy localhost', () => {
    expect(resolveTenantCode('localhost', 'demo')).toBe('demo');
  });

  it('không có tenant ở tên miền gốc hoặc www', () => {
    expect(resolveTenantCode('contrack.vn')).toBeNull();
    expect(resolveTenantCode('www.contrack.vn')).toBeNull();
  });
});
