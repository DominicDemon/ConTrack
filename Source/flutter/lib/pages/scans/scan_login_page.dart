import 'package:flutter/material.dart';
import '../../services/scans/customer_scan_api.dart';

/// Customer portal sign in for the scan area. It uses the same account as the customer web portal.
class ScanLoginPage extends StatefulWidget {
  final bool returnWhenDone;
  const ScanLoginPage({super.key, this.returnWhenDone = false});
  @override
  State<ScanLoginPage> createState() => _ScanLoginPageState();
}

class _ScanLoginPageState extends State<ScanLoginPage> {
  final _phone = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false, _show = false;
  String? _error;
  @override
  void initState() { super.initState(); _resume(); }
  @override
  void dispose() { _phone.dispose(); _password.dispose(); super.dispose(); }

  Future<void> _resume() async {
    try {
      final session = await CustomerScanApi.instance.me();
      if (!mounted) return;
      if (session['mustChangePassword'] == true) {
        setState(() => _error = 'Hãy đổi mật khẩu tạm trên cổng khách hàng web trước khi tiếp tục.');
      } else if (!widget.returnWhenDone) {
        Navigator.of(context).pushReplacementNamed('/scans/contracts');
      }
    } catch (_) {}
  }

  Future<void> _submit() async {
    if (_phone.text.trim().isEmpty || _password.text.isEmpty) { setState(() => _error = 'Nhập số điện thoại và mật khẩu.'); return; }
    setState(() { _busy = true; _error = null; });
    try {
      final session = await CustomerScanApi.instance.login(_phone.text.trim(), _password.text);
      if (!mounted) return;
      if (session['mustChangePassword'] == true) {
        setState(() => _error = 'Hãy đổi mật khẩu tạm trên cổng khách hàng web trước khi tiếp tục.');
        await CustomerScanApi.instance.logout();
        return;
      }
      if (widget.returnWhenDone) { Navigator.of(context).pop(true); } else { Navigator.of(context).pushReplacementNamed('/scans/contracts'); }
    } on CustomerScanException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(appBar: AppBar(title: const Text('Đăng nhập bản scan')), body: ListView(padding: const EdgeInsets.all(24), children: [
      Icon(Icons.document_scanner_outlined, size: 48, color: theme.colorScheme.primary),
      const SizedBox(height: 12), Text('Cổng khách hàng', style: theme.textTheme.headlineSmall, textAlign: TextAlign.center),
      const SizedBox(height: 8), Text('Dùng số điện thoại và mật khẩu như trên web khách hàng.', textAlign: TextAlign.center, style: theme.textTheme.bodyMedium),
      const SizedBox(height: 28), TextField(controller: _phone, keyboardType: TextInputType.phone, autofillHints: const [AutofillHints.telephoneNumber], decoration: const InputDecoration(labelText: 'Số điện thoại', border: OutlineInputBorder())),
      const SizedBox(height: 16), TextField(controller: _password, obscureText: !_show, onSubmitted: (_) => _submit(), decoration: InputDecoration(labelText: 'Mật khẩu', border: const OutlineInputBorder(), suffixIcon: IconButton(onPressed: () => setState(() => _show = !_show), icon: Icon(_show ? Icons.visibility_off : Icons.visibility)))),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(_error!, style: TextStyle(color: theme.colorScheme.error))),
      const SizedBox(height: 20), FilledButton(onPressed: _busy ? null : _submit, child: Text(_busy ? 'Đang đăng nhập…' : 'Đăng nhập')),
    ]));
  }
}
