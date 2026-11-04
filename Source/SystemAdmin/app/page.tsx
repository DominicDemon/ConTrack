import InternalLogin from "@/components/internal/internal-login";
import SystemAdminLogin from "@/components/auth/system-admin-login-page";
export default function LoginPage() { return process.env.APPLICATION_MODE === "Legacy" ? <SystemAdminLogin/> : <InternalLogin/>; }
