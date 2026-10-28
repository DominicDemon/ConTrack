// node --test scripts/payment-receipt.test.mjs
import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";
import ts from "typescript";

function load(file, globals = {}) {
  const exports = {};
  vm.runInNewContext(ts.transpileModule(fs.readFileSync(new URL(`../${file}`, import.meta.url), "utf8"), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText, { exports, URL, URLSearchParams, AbortController, queueMicrotask, ...globals });
  return exports;
}
const invoice = "/billing/invoices/0e330139-1f17-45dc-b51d-8455b15298db";

test("returnTo survives login, required password change, refresh and second login", () => {
  const window = { location: { pathname: "/", search: `?returnTo=${encodeURIComponent(invoice)}&tenantCode=buyer-org` } };
  const api = load("lib/auth-return.ts", { window });
  const change = api.currentAuthHref("/change-password", { required: "1" });
  assert.equal(new URL(change, "https://app.test").searchParams.get("returnTo"), invoice);
  let refreshed = new URL(change, "https://app.test");
  window.location = { pathname: refreshed.pathname, search: refreshed.search };
  const relogin = api.currentAuthHref("/", { password_changed: "1" });
  refreshed = new URL(relogin, "https://app.test");
  assert.equal(refreshed.searchParams.get("tenantCode"), "buyer-org");
  assert.equal(api.getAuthReturnPath(refreshed.searchParams.get("returnTo")), invoice);
  window.location = { pathname: invoice, search: "?tenantCode=buyer-org" };
  const expired = new URL(api.currentAuthHref("/", { error: "session_expired" }), "https://app.test");
  assert.equal(expired.searchParams.get("returnTo"), invoice);
  assert.equal(expired.searchParams.get("tenantCode"), "buyer-org");
});

test("rejects external, encoded, nested and forged destinations; preserves pricing", () => {
  const { getAuthReturnPath: safe } = load("lib/auth-return.ts");
  for (const value of ["https://evil.test", "//evil.test", "/\\evil.test", "javascript:alert(1)", `${invoice}?returnTo=//evil`,
    `${invoice}#fragment`, "/billing/invoices/fake", "/pricing?returnTo=https://evil", "/pricing?plan=paid&months=3&months=24",
    "/pricing?plan=%70aid", "/pricing?plan=paid&months=25", `${invoice}/../fake`, "/%2f/evil", "/pricing?plan=paid%26returnTo=evil"]) assert.equal(safe(value), null, value);
  assert.equal(safe(invoice), invoice);
  assert.equal(safe("/billing/invoices"), "/billing/invoices");
  assert.equal(safe("/billing/invoices?returnTo=//evil"), null);
  assert.equal(safe("/pricing?plan=trial"), "/pricing?plan=trial");
  assert.equal(safe("/pricing?plan=paid&months=24"), "/pricing?plan=paid&months=24");
});

const flush = async () => { for (let n = 0; n < 10; n++) await Promise.resolve(); };
function harness(request) {
  let now = 0, seq = 0;
  const timers = new Map(), listeners = new Set(), values = [], errors = [];
  let timeouts = 0;
  const document = { visibilityState: "visible", addEventListener: (_, fn) => listeners.add(fn), removeEventListener: (_, fn) => listeners.delete(fn) };
  const { startStatusPoller } = load("lib/status-poller.ts", {
    document, Date: { now: () => now }, setTimeout: (fn, delay) => { timers.set(++seq, { fn, at: now + delay }); return seq; }, clearTimeout: id => timers.delete(id),
  });
  const start = () => startStatusPoller({ request, terminal: x => x.status !== "PENDING", onValue: x => values.push(x), onError: x => errors.push(x), onTimeout: () => timeouts++ });
  return { start, values, errors, timers, get timeouts() { return timeouts; }, foreground() { listeners.forEach(fn => fn()); },
    async advance(ms) { const end = now + ms; while (true) { const next = [...timers.entries()].filter(([, t]) => t.at <= end).sort((a, b) => a[1].at - b[1].at)[0]; if (!next) break; timers.delete(next[0]); now = next[1].at; next[1].fn(); await flush(); } now = end; await flush(); } };
}

test("polls after completion with no overlap; foreground checks and READY stop", async () => {
  const pending = []; let calls = 0;
  const h = harness(signal => { calls++; return new Promise(resolve => pending.push({ signal, resolve })); });
  const stop = h.start(); await flush();
  h.foreground(); await h.advance(8_000); assert.equal(calls, 1);
  pending[0].resolve({ status: "PENDING", isPaid: true }); await flush(); await h.advance(0);
  assert.equal(calls, 2); // one queued foreground check after the first request completes
  pending[1].resolve({ status: "PENDING" }); await flush();
  await h.advance(3999); assert.equal(calls, 2);
  await h.advance(1); assert.equal(calls, 3);
  pending[2].resolve({ status: "READY" }); await flush();
  await h.advance(20_000); h.foreground(); assert.equal(calls, 3); assert.equal(h.timers.size, 0); stop();
});

test("network errors retry, deadline aborts and a new attempt restarts without F5", async () => {
  let calls = 0, signal;
  const h = harness(s => { signal = s; calls++; return calls === 1 ? Promise.reject(new Error("offline")) : new Promise(() => {}); });
  h.start(); await flush(); assert.equal(h.errors.length, 1);
  await h.advance(4_000); assert.equal(calls, 2);
  await h.advance(15 * 60_000); assert.equal(h.timeouts, 1); assert.equal(signal.aborted, true);
  const stop = h.start(); await flush(); assert.equal(calls, 3); stop(); assert.equal(signal.aborted, true);
});

test("Strict Mode cleanup dispatches no stale request and ignores stale completion", async () => {
  let calls = 0, complete;
  const h = harness(() => { calls++; return new Promise(resolve => { complete = resolve; }); });
  h.start()(); const cleanup = h.start(); await flush(); assert.equal(calls, 1);
  cleanup(); complete({ status: "READY" }); await flush(); assert.equal(h.values.length, 0);
  assert.equal(h.timers.size, 0);
});

test("provisioning failure, cancellation and invalid links stop polling", async () => {
  for (const status of ["PROVISIONING_FAILED", "CANCELLED", "PAYMENT_FAILED", "RECONCILIATION_REQUIRED"]) {
    const h = harness(async () => ({ status })); h.start(); await flush(); assert.equal(h.timers.size, 0);
  }
  const h = harness(async () => { throw { response: { status: 404 } }; }); h.start(); await flush(); assert.equal(h.timers.size, 0);
});
