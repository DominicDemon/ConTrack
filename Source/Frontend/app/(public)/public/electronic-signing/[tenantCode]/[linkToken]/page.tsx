import Link from "next/link";

export default function CustomerSigningLinkNotice() {
  return <main style={{ maxWidth: 640, margin: "12vh auto", padding: 24, fontFamily: "system-ui, sans-serif" }}>
    <p style={{ color: "#18756e", fontSize: 13, fontWeight: 700, letterSpacing: 1 }}>CỔNG KHÁCH HÀNG</p>
    <h1>Vui lòng ký trong Portal</h1>
    <p>Để bảo vệ hợp đồng, khách hàng cần đăng nhập tài khoản Portal, mở hợp đồng đã được công ty ký và xác minh OTP trước khi ký.</p>
    <div style={{ display: "flex", flexWrap: "wrap", gap: 12, marginTop: 24 }}>
      <Link href="/portal/login" style={{ padding: "10px 16px", borderRadius: 7, background: "#18756e", color: "white", textDecoration: "none" }}>Đăng nhập Portal</Link>
      <Link href="/portal/contracts" style={{ padding: "10px 16px", borderRadius: 7, border: "1px solid #d5dfde", color: "#193839", textDecoration: "none" }}>Hợp đồng của tôi</Link>
    </div>
  </main>;
}
