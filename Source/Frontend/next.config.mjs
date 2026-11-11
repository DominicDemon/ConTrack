import { readFileSync } from "node:fs";

const branding = JSON.parse(readFileSync(new URL("../Shared/application-branding.json", import.meta.url), "utf8"));
if (typeof branding.name !== "string" || !branding.name.trim() || branding.name !== branding.name.trim() || /[\r\n]/.test(branding.name)) {
  throw new Error("Shared application name must be nonblank, trimmed and single-line.");
}

/** @type {import('next').NextConfig} */
const nextConfig = {
  env: { NEXT_PUBLIC_APP_NAME: branding.name },
  output: "standalone",
  experimental: { proxyClientMaxBodySize: "48mb" },
  distDir: process.env.NEXT_DIST_DIR || ".next",
  async rewrites() {
    const api = process.env.INTERNAL_API_ORIGIN || "http://127.0.0.1:5005";
    return [{ source: "/api/internal/:path*", destination: `${api}/api/internal/:path*` },
      { source: "/api/customer/:path*", destination: `${api}/api/customer/:path*` }];
  },
  // Port 3000 is the customer portal only; internal screens live in SystemAdmin (3001). Static files (with a dot) pass through.
  async redirects() {
    return [{ source: "/", destination: "/portal/contracts", permanent: false },
      { source: "/:path((?!portal(?:/|$)|public/|api/|_next/)[^.]*)", destination: "/portal/contracts", permanent: false }];
  },
  async headers() {
    return ["/verify-recovery-email", "/forgot-password", "/reset-password"].map(source => ({ source, headers: [
      { key: "Referrer-Policy", value: "no-referrer" },
      { key: "Cache-Control", value: "no-store" },
    ] }));
  },
  images: {
    unoptimized: true,
  },
}

export default nextConfig
