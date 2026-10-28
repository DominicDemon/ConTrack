// Uses a running FE with isolated browser/API fixtures; never sends email or writes real data.
// PLAYWRIGHT_MODULE may point to an installed Playwright package outside the repository.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const id = '0e330139-1f17-45dc-b51d-8455b15298db';
const target = `/billing/invoices/${id}`;
const origin = process.env.FRONTEND_TEST_URL || 'http://localhost:3000';
const output = path.resolve('../Artifacts/Mockups/payment-confirmation-tests');
fs.mkdirSync(output, { recursive: true });

(async () => {
  const browser = await chromium.launch({ headless: true, channel: process.env.PLAYWRIGHT_CHANNEL || "msedge" });
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    let logged = false, mustChange = true, deny = false, readinessCalls = 0, invalidLogin = true, invoiceError = 0;
    const profile = () => ({ employeeId: 1, account: 'buyer@example.test', fullName: 'Nguyễn Văn A', employeeType: 6,
      roleName: 'Manager', tenantId: 1, tenantCode: 'buyer-org', tenantName: 'Công ty thử nghiệm',
      permissionVersion: 'rbac-v2', permissions: [], mustChangePassword: mustChange, defaultPage: '/contracts' });
    const receipt = { publicId: id, invoiceCode: 'INV-202610-TEST01', status: 'PAID', billingName: 'Công ty thử nghiệm DTC',
      buyerName: 'Nguyễn Văn A', billingEmail: 'buyer@example.test', billingPhone: '0912345678', billingAddress: null,
      billingTaxIdentifier: null, planName: '12 tháng', durationMonths: 12, durationDays: 360,
      subtotal: 2880000, discountAmount: 288000, totalAmount: 2592000, currency: 'VND',
      paidAt: '2026-10-28T07:30:00Z', paymentMethod: 'PAYOS', paymentReference: 'FT-TEST-123456' };
    await context.route('**/api/**', async route => {
      const url = new URL(route.request().url());
      let status = 200, data = {};
      if (route.request().method() === 'OPTIONS') return route.fulfill({ status: 204, headers: {
        'access-control-allow-origin': origin, 'access-control-allow-credentials': 'true',
        'access-control-allow-headers': 'content-type,x-tenant-code', 'access-control-allow-methods': 'GET,POST,PUT,OPTIONS' } });
      if (url.pathname.endsWith('/readiness')) {
        readinessCalls++;
        data = readinessCalls === 1 ? { status: 'PENDING', isPaid: true } : { status: 'READY', isPaid: true, tenantCode: 'buyer-org', returnTo: target };
      } else if (url.pathname.endsWith('/auth/me')) {
        if (!logged) { status = 401; data = { code: 'AuthenticationRequired' }; } else data = profile();
      } else if (url.pathname.endsWith('/auth/login')) {
        if (invalidLogin) { invalidLogin = false; status = 401; data = { message: 'Sai mật khẩu thử nghiệm' }; }
        else { logged = true; data = { message: 'Đăng nhập thành công' }; }
      } else if (url.pathname.endsWith('/auth/password')) { mustChange = false; logged = false; }
      else if (url.pathname.endsWith('/auth/logout')) { logged = false; }
      else if (url.pathname === '/api/billing/invoices') data = { items: [{ ...receipt, startAt: '2026-10-28T07:30:00Z', endAt: '2027-10-28T07:30:00Z' }], total: 1, page: 1, pageSize: 20 };
      else if (url.pathname.includes('/billing/invoices/')) { if (invoiceError || !logged || mustChange) { status = invoiceError || (!logged ? 401 : 403); if (status === 401) logged = false; else mustChange = true; data = { code: status === 401 ? 'AuthenticationRequired' : 'MustChangePassword' }; invoiceError = 0; } else if (deny) { status = 404; data = {}; } else data = receipt; }
      else if (url.pathname.includes('/notifications')) data = { items: [], unreadCount: 0, nextCursor: null };
      return route.fulfill({ status, contentType: 'application/json', headers: { 'access-control-allow-origin': origin, 'access-control-allow-credentials': 'true' }, body: JSON.stringify(data) });
    });
    await context.route('**/hubs/**', route => route.abort());
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(`${origin}/billing/receipts/${id}`);
    await page.getByText('Đã thanh toán, đang tạo tài khoản…').waitFor();
    await page.waitForURL(url => url.pathname === '/' && url.searchParams.get('returnTo') === target);
    await page.reload();
    // Use visible form fields in their displayed order (tenant, account, password).
    const login = async () => {
      await page.waitForFunction(() => document.querySelector('button[type="submit"]')?.disabled === false);
      await page.locator('input').nth(0).fill('buyer-org');
      await page.locator('input').nth(1).fill('buyer@example.test');
      await page.locator('input').nth(2).fill('Temporary12345');
      await page.locator('button[type="submit"]').click();
    };
    await login();
    await page.getByText('Sai mật khẩu thử nghiệm').waitFor();
    assert.equal(new URL(page.url()).searchParams.get('returnTo'), target);
    await page.reload(); await login();
    await page.waitForURL(url => url.pathname === '/change-password');
    assert.equal(new URL(page.url()).searchParams.get('returnTo'), target);
    await page.reload();
    await page.locator('#change-password-form input').nth(0).fill('Temporary12345');
    await page.locator('#change-password-form input').nth(1).fill('NewPassword12345');
    await page.locator('#change-password-form input').nth(2).fill('NewPassword12345');
    await page.getByRole('button', { name: 'Đổi mật khẩu và đăng xuất' }).click();
    await page.waitForURL(url => url.pathname === '/');
    assert.equal(new URL(page.url()).searchParams.get('returnTo'), target);
    assert.equal(new URL(page.url()).searchParams.get('tenantCode'), 'buyer-org');
    await page.reload(); await login();
    await page.waitForURL(url => url.pathname === target);
    await page.locator('[data-payment-receipt]').waitFor();
    await page.locator('[data-sonner-toast]').first().waitFor({ state: 'hidden' });
    await page.screenshot({ path: path.join(output, 'desktop.png'), fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'mobile.png'), fullPage: true });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
    await page.evaluate(() => { document.documentElement.classList.add('dark'); });
    await page.screenshot({ path: path.join(output, 'dark-mobile.png'), fullPage: true });
    await page.setViewportSize({ width: 794, height: 1123 });
    await page.emulateMedia({ media: 'print' });
    await page.screenshot({ path: path.join(output, 'print.png'), fullPage: true });
    assert.equal(await page.getByRole('button', { name: 'In / Lưu PDF' }).isVisible(), false);
    await page.emulateMedia({ media: 'screen' });
    await page.getByRole('link', { name: 'Lịch sử thanh toán' }).click();
    await page.waitForURL(url => url.pathname === '/billing/invoices');
    await page.getByRole('link', { name: 'Xem chứng từ INV-202610-TEST01' }).waitFor();
    await page.getByRole('button', { name: 'Menu tài khoản' }).click();
    await page.getByRole('menuitem', { name: 'Gói dịch vụ & Thanh toán' }).waitFor();
    await page.getByRole('menu').evaluate(async element => { await Promise.all(element.getAnimations().map(animation => animation.finished)); });
    await page.screenshot({ path: path.join(output, 'billing-menu.png'), fullPage: true });
    await page.getByRole('menuitem', { name: 'Gói dịch vụ & Thanh toán' }).click();
    await page.getByRole('menu').waitFor({ state: 'hidden' });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: path.join(output, 'billing-history-mobile.png'), fullPage: true });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true);
    await page.getByRole('link', { name: 'Xem chứng từ INV-202610-TEST01' }).click();
    await page.locator('[data-payment-receipt]').waitFor();
    await page.goto(`${origin}${target}?tenantCode=buyer-org`);
    await page.locator('[data-payment-receipt]').waitFor();
    invoiceError = 401; await page.reload();
    await page.waitForURL(url => url.pathname === '/');
    assert.equal(new URL(page.url()).searchParams.get('returnTo'), target);
    assert.equal(new URL(page.url()).searchParams.get('tenantCode'), 'buyer-org');
    await login(); await page.waitForURL(url => url.pathname === target);
    await page.locator('[data-payment-receipt]').waitFor();
    invoiceError = 403; await page.reload();
    await page.waitForURL(url => url.pathname === '/change-password');
    assert.equal(new URL(page.url()).searchParams.get('returnTo'), target);
    assert.equal(new URL(page.url()).searchParams.get('tenantCode'), 'buyer-org');
    mustChange = false;
    await page.goto(`${origin}${target}?tenantCode=buyer-org`);
    await page.locator('[data-payment-receipt]').waitFor();
    deny = true; await page.reload();
    await page.getByRole('heading', { name: 'Không có quyền xem chứng từ' }).waitFor();
    assert.equal(logged, true); // Viewing an unauthorized receipt must not log out the current user.
    await page.getByRole('button', { name: 'Đăng nhập bằng tài khoản khác' }).click();
    await page.waitForURL(url => url.pathname === '/');
    assert.equal(new URL(page.url()).searchParams.get('returnTo'), target);
    assert.deepEqual(errors, []);
    console.log('PASS: polling, bad login, refresh at every auth step, forced password change, receipt, mobile/dark/print, wrong-account handling.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });

