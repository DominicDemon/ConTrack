import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../services/scans/internal_api_client.dart';
import '../../services/scans/signed_scan_api.dart';
import '../../widgets/app_text_field.dart';

/// Signs in to the internal API. With [returnWhenDone] it pops `true` instead of
/// opening the list, so an expired session does not cost the pages being uploaded.
class ScanLoginPage extends StatefulWidget {
  final bool returnWhenDone;

  const ScanLoginPage({super.key, this.returnWhenDone = false});

  @override
  State<ScanLoginPage> createState() => _ScanLoginPageState();
}

class _ScanLoginPageState extends State<ScanLoginPage> {
  final _usernameController = TextEditingController();
  final _passwordController = TextEditingController();

  bool _isChecking = true;
  bool _isSubmitting = false;
  bool _showPassword = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _openIfSignedIn();
  }

  @override
  void dispose() {
    _usernameController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  Future<void> _openIfSignedIn() async {
    if (widget.returnWhenDone) {
      setState(() => _isChecking = false);
      return;
    }
    try {
      final access = await SignedScanApi.access();
      if (mounted && access.canView) {
        Navigator.of(context).pushReplacementNamed('/scans/contracts');
        return;
      }
    } catch (_) {
      // Not signed in yet: show the form.
    }
    if (mounted) setState(() => _isChecking = false);
  }

  Future<void> _submit() async {
    final username = _usernameController.text.trim();
    final password = _passwordController.text;
    if (username.isEmpty || password.isEmpty) {
      setState(() => _error = 'Vui lòng nhập tên đăng nhập và mật khẩu.');
      return;
    }

    TextInput.finishAutofillContext();
    setState(() {
      _isSubmitting = true;
      _error = null;
    });
    try {
      await SignedScanApi.login(username: username, password: password);
      final access = await SignedScanApi.access();
      if (!mounted) return;
      if (!access.canView) {
        await SignedScanApi.logout();
        setState(() => _error = 'Tài khoản chưa được cấp quyền xem bản ký giấy. Hãy báo quản trị.');
      } else if (widget.returnWhenDone) {
        Navigator.of(context).pop(true);
      } else {
        Navigator.of(context).pushReplacementNamed('/scans/contracts');
      }
    } on InternalApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: theme.colorScheme.surface,
      appBar: AppBar(title: const Text('Đăng nhập bản ký giấy'), scrolledUnderElevation: 0),
      body: _isChecking
          ? const Center(child: CircularProgressIndicator())
          : AutofillGroup(
              child: ListView(
                padding: const EdgeInsets.fromLTRB(24, 16, 24, 32),
                children: [
                  Text(
                    'Dùng tài khoản nhân viên trên hệ thống nội bộ.',
                    style: theme.textTheme.bodyMedium,
                  ),
                  const SizedBox(height: 24),
                  const Text('Tên đăng nhập', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
                  const SizedBox(height: 8),
                  AppTextField(
                    controller: _usernameController,
                    placeholder: 'Nhập tên đăng nhập',
                    autocorrect: false,
                    enableSuggestions: false,
                    autofillHints: const [AutofillHints.username],
                  ),
                  const SizedBox(height: 16),
                  const Text('Mật khẩu', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600)),
                  const SizedBox(height: 8),
                  AppTextField(
                    controller: _passwordController,
                    obscureText: !_showPassword,
                    placeholder: '••••••••',
                    textInputAction: TextInputAction.done,
                    autofillHints: const [AutofillHints.password],
                    onSubmitted: (_) => _submit(),
                    suffixIcon: IconButton(
                      icon: Icon(_showPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined),
                      tooltip: _showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu',
                      onPressed: () => setState(() => _showPassword = !_showPassword),
                    ),
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: 12),
                    Text(_error!, style: TextStyle(color: theme.colorScheme.error, fontWeight: FontWeight.w500)),
                  ],
                  const SizedBox(height: 24),
                  FilledButton(
                    onPressed: _isSubmitting ? null : _submit,
                    style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
                    child: Text(_isSubmitting ? 'Đang xử lý…' : 'Đăng nhập'),
                  ),
                ],
              ),
            ),
    );
  }
}
