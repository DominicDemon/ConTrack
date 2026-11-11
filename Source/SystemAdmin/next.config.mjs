import { readFileSync } from "node:fs";

const branding = JSON.parse(readFileSync(new URL("../Shared/application-branding.json", import.meta.url), "utf8"));
if (typeof branding.name !== "string" || !branding.name.trim() || branding.name !== branding.name.trim() || /[\r\n]/.test(branding.name)) {
  throw new Error("Shared application name must be nonblank, trimmed and single-line.");
}

/** @type {import('next').NextConfig} */
const nextConfig = {
  env: { NEXT_PUBLIC_APP_NAME: branding.name },
  output: "standalone",
  async rewrites() {
    const api = process.env.INTERNAL_API_ORIGIN || "http://127.0.0.1:5005";
    return [{ source: "/api/internal/:path*", destination: `${api}/api/internal/:path*` }];
  },
  async redirects() {
    return [
      { source: "/dashboard", destination: "/workspace", permanent: false },
      { source: "/tenants", destination: "/workspace", permanent: false },
      { source: "/audit-logs", destination: "/workspace", permanent: false },
      { source: "/system-health", destination: "/workspace", permanent: false },
      { source: "/profile", destination: "/workspace", permanent: false },
      { source: "/support-tickets", destination: "/workspace/support-tickets", permanent: false },
      { source: "/support-tickets/:id", destination: "/workspace/support-tickets/:id", permanent: false },
    ];
  },
  images: {
    unoptimized: true,
  },
}

export default nextConfig
