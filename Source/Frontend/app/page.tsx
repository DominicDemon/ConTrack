import InternalLogin from "@/components/internal/internal-login";
import LegacyLogin from "@/components/auth/legacy-login-page";
export default function LoginPage() { return process.env.APPLICATION_MODE === "Legacy" ? <LegacyLogin/> : <InternalLogin/>; }
