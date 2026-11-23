import 'dart:typed_data';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import '../../models/scans/signed_scan_dto.dart';
import '../../services/scans/customer_scan_api.dart';
import 'signed_contract_detail_page.dart';

/// Customer scan list mirrors /portal/scans: only the customer's own available contract scans.
class SignedContractListPage extends StatefulWidget {
  const SignedContractListPage({super.key});
  @override
  State<SignedContractListPage> createState() => _SignedContractListPageState();
}

class _SignedContractListPageState extends State<SignedContractListPage> {
  final _search = TextEditingController();
  final _key = TextEditingController(text: RememberedFileKey.value);
  List<CustomerScan>? _scans;
  List<SignedContractItem>? _contracts;
  bool? _hasKey;
  String? _error;
  bool _loading = false, _showKey = false;
  bool _showContracts = true;

  @override
  void initState() { super.initState(); _load(); }
  @override
  void dispose() { _search.dispose(); _key.dispose(); super.dispose(); }

  Future<void> _load() async {
    if (_loading) return;
    setState(() { _loading = true; _error = null; });
    try {
      final result = await Future.wait([CustomerScanApi.instance.list(), CustomerScanApi.instance.hasKey(), CustomerScanApi.instance.contracts()]);
      if (mounted) setState(() { _scans = result[0] as List<CustomerScan>; _hasKey = result[1] as bool; _contracts = result[2] as List<SignedContractItem>; });
    } on CustomerScanException catch (e) {
      if (!mounted) return;
      if (e.isUnauthorized) { Navigator.of(context).pushNamedAndRemoveUntil('/scans/login', (route) => route.isFirst); }
      else { setState(() => _error = e.message); }
    } finally { if (mounted) setState(() => _loading = false); }
  }

  List<CustomerScan> get _visible => (_scans ?? []).where((scan) => '${scan.fileName} ${scan.contractCode ?? ''} ${scan.contractName ?? ''}'.toLowerCase().contains(_search.text.trim().toLowerCase())).toList();
  List<SignedContractItem> get _visibleContracts => (_contracts ?? []).where((contract) => '${contract.documentCode ?? ''} ${contract.documentName}'.toLowerCase().contains(_search.text.trim().toLowerCase())).toList();

  Future<void> _openContract(SignedContractItem contract) async {
    await Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => SignedContractDetailPage(contract: contract)));
    if (mounted) _load();
  }

  Future<void> _open(CustomerScan scan) async {
    if (_key.text.trim().isEmpty) { setState(() => _error = 'Nhập key xem bản scan của bạn.'); return; }
    setState(() => _error = null);
    try {
      final response = await CustomerScanApi.instance.open(scan.fileId, _key.text.trim());
      if (!mounted) return;
      final contentType = response.headers['content-type'] ?? 'application/octet-stream';
      if (!contentType.startsWith('image/')) {
        await FilePicker.saveFile(fileName: scan.fileName, bytes: response.bodyBytes);
        if (mounted) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Đã tải bản scan về thiết bị.')));
        return;
      }
      await showDialog<void>(context: context, builder: (context) => AlertDialog(
        title: Text(scan.fileName, maxLines: 2, overflow: TextOverflow.ellipsis),
        content: SizedBox(width: 520, child: InteractiveViewer(child: Image.memory(Uint8List.fromList(response.bodyBytes), fit: BoxFit.contain))),
        actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Đóng'))],
      ));
    } on CustomerScanException catch (e) { if (mounted) setState(() => _error = e.message); }
  }

  Future<void> _logout() async { await CustomerScanApi.instance.logout(); if (mounted) Navigator.of(context).pushNamedAndRemoveUntil('/scans/login', (route) => route.isFirst); }

  Future<void> _createKey({bool reset = false}) async {
    final password = TextEditingController();
    final code = TextEditingController();
    final newKey = TextEditingController();
    final passwordOk = await showDialog<bool>(context: context, builder: (context) => AlertDialog(title: Text(reset ? 'Đặt lại key xem bản scan' : 'Tạo key xem bản scan'), content: TextField(controller: password, obscureText: true, decoration: const InputDecoration(labelText: 'Mật khẩu tài khoản')), actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Gửi mã'))]));
    if (passwordOk != true || !mounted) { password.dispose(); code.dispose(); newKey.dispose(); return; }
    try {
      final email = await CustomerScanApi.instance.sendKeyCode(password.text);
      if (!mounted) return;
      final confirmed = await showDialog<bool>(context: context, builder: (context) => AlertDialog(title: const Text('Nhập mã xác nhận'), content: Column(mainAxisSize: MainAxisSize.min, children: [Text('Mã đã gửi tới $email'), TextField(controller: code, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Mã xác nhận')), TextField(controller: newKey, obscureText: true, decoration: const InputDecoration(labelText: 'Key mới'))]), actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(reset ? 'Đặt lại key' : 'Tạo key'))]));
      if (confirmed == true) { await CustomerScanApi.instance.setKey(password.text, code.text.trim(), newKey.text); await _load(); }
    } on CustomerScanException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { password.dispose(); code.dispose(); newKey.dispose(); }
  }

  Future<void> _changeKey() async {
    final current = TextEditingController(); final next = TextEditingController();
    try {
      final ok = await showDialog<bool>(context: context, builder: (context) => AlertDialog(title: const Text('Đổi key xem bản scan'), content: Column(mainAxisSize: MainAxisSize.min, children: [TextField(controller: current, obscureText: true, decoration: const InputDecoration(labelText: 'Key hiện tại')), TextField(controller: next, obscureText: true, decoration: const InputDecoration(labelText: 'Key mới'))]), actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Đổi key'))]));
      if (ok == true) { await CustomerScanApi.instance.changeKey(current.text, next.text); if (mounted) { setState(() { _key.clear(); _error = null; }); ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Đã đổi key. Nhập key mới để mở bản scan.'))); } }
    } on CustomerScanException catch (e) { if (mounted) setState(() => _error = e.message); }
    finally { current.dispose(); next.dispose(); }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context), muted = theme.colorScheme.onSurface.withValues(alpha: .62);
    return Scaffold(appBar: AppBar(title: const Text('Bản scan hợp đồng'), actions: [IconButton(onPressed: _loading ? null : _load, tooltip: 'Tải lại', icon: const Icon(Icons.refresh)), IconButton(onPressed: _logout, tooltip: 'Đăng xuất', icon: const Icon(Icons.logout))]),
      body: RefreshIndicator(onRefresh: _load, child: ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.all(16), children: [
        Text('Bản giấy của hợp đồng đã ký với công ty: chọn hợp đồng để tải lên hoặc chụp, rồi mở lại tại đây. Nhân viên phụ trách hợp đồng cũng xem được.', style: TextStyle(color: muted)),
        const SizedBox(height: 12),
        Card(color: theme.colorScheme.primaryContainer.withValues(alpha: .45), child: ListTile(
          leading: const Icon(Icons.inventory_2_outlined), title: const Text('Kho lưu trữ riêng'),
          subtitle: const Text('Scan và lưu tài liệu bất kỳ, kể cả hợp đồng với đối tác khác. Chỉ bạn xem được.'),
          trailing: const Icon(Icons.chevron_right),
          onTap: () async { await Navigator.of(context).pushNamed('/scans/vault'); if (mounted) setState(() { if (_key.text.isEmpty) _key.text = RememberedFileKey.value; }); })),
        const SizedBox(height: 12),
        if (_hasKey == false) Card(child: Padding(padding: const EdgeInsets.all(16), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Row(children: [const Icon(Icons.key_outlined), const SizedBox(width: 12), Expanded(child: Text('Bạn chưa có key xem bản scan.', style: TextStyle(color: muted)))]), const SizedBox(height: 8), Align(alignment: Alignment.centerRight, child: TextButton.icon(onPressed: _createKey, icon: const Icon(Icons.add), label: const Text('Tạo key')))]))),
        if (_hasKey == true) Card(child: Padding(padding: const EdgeInsets.all(14), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('Key xem bản scan', style: theme.textTheme.labelLarge), const SizedBox(height: 8), TextField(controller: _key, obscureText: !_showKey, decoration: InputDecoration(isDense: true, border: const OutlineInputBorder(), suffixIcon: IconButton(onPressed: () => setState(() => _showKey = !_showKey), icon: Icon(_showKey ? Icons.visibility_off : Icons.visibility)))), Align(alignment: Alignment.centerRight, child: Wrap(children: [TextButton(onPressed: _changeKey, child: const Text('Đổi key')), TextButton(onPressed: () => _createKey(reset: true), child: const Text('Quên key?'))])), if (RememberedFileKey.value.isNotEmpty) Text('Key được nhớ trên máy này ${RememberedFileKey.minutes} phút kể từ lần dùng cuối.', style: TextStyle(fontSize: 11, color: muted))]))),
        const SizedBox(height: 12),
        SegmentedButton<bool>(showSelectedIcon: false, segments: const [ButtonSegment(value: true, label: Text('Hợp đồng của tôi')), ButtonSegment(value: false, label: Text('Bản scan'))], selected: {_showContracts}, onSelectionChanged: (value) => setState(() => _showContracts = value.first)),
        const SizedBox(height: 12), TextField(controller: _search, onChanged: (_) => setState(() {}), decoration: InputDecoration(prefixIcon: const Icon(Icons.search), hintText: _showContracts ? 'Tìm theo mã hoặc tên hợp đồng' : 'Tìm theo tệp hoặc hợp đồng', border: const OutlineInputBorder(), isDense: true)),
        if (_error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(_error!, style: TextStyle(color: theme.colorScheme.error))),
        const SizedBox(height: 12),
        if ((_showContracts ? _contracts : _scans) == null && _error == null) const Padding(padding: EdgeInsets.all(36), child: Center(child: CircularProgressIndicator())),
        if (_showContracts && _contracts != null && _visibleContracts.isEmpty) _empty(muted, _search.text.trim().isEmpty ? 'Bạn chưa có hợp đồng nào với công ty' : 'Chưa có hợp đồng nào phù hợp', _search.text.trim().isEmpty ? 'Muốn lưu tài liệu riêng? Dùng Kho lưu trữ riêng ở trên.' : ''),
        if (_showContracts) ..._visibleContracts.map((contract) => Card(margin: const EdgeInsets.only(bottom: 10), child: ListTile(title: Text(contract.documentCode ?? 'Chưa cấp mã'), subtitle: Text('${contract.documentName}\n${SignedScanLabels.contractStatus(contract.contractStatus)}'), isThreeLine: true, trailing: const Icon(Icons.chevron_right), onTap: () => _openContract(contract)))),
        if (!_showContracts && _scans == null && _error == null) const Padding(padding: EdgeInsets.all(36), child: Center(child: CircularProgressIndicator())),
        if (!_showContracts && _scans?.isEmpty == true) _empty(muted, 'Chưa có bản scan nào', 'Bản scan hợp đồng sẽ hiện ở đây khi được chia sẻ với bạn.'),
        if (!_showContracts && _scans != null && _scans!.isNotEmpty && _visible.isEmpty) _empty(muted, 'Không tìm thấy bản scan phù hợp', ''),
        if (!_showContracts) ..._visible.map((scan) => Card(margin: const EdgeInsets.only(bottom: 10), child: Padding(padding: const EdgeInsets.all(14), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [const Icon(Icons.description_outlined), const SizedBox(width: 10), Expanded(child: Text(scan.fileName, style: theme.textTheme.titleSmall, overflow: TextOverflow.ellipsis)), _Status(ready: scan.ready, status: scan.status)]),
          const SizedBox(height: 8), Text('${scan.contractCode ?? 'Hợp đồng'}${scan.contractName == null ? '' : ' · ${scan.contractName}'}', style: TextStyle(color: muted)),
          const SizedBox(height: 4), Text('Lưu ngày ${_date(scan.uploadedAt)}', style: TextStyle(fontSize: 12, color: muted)),
          if (scan.ready) Align(alignment: Alignment.centerRight, child: TextButton.icon(onPressed: _hasKey == true ? () => _open(scan) : null, icon: const Icon(Icons.visibility_outlined), label: const Text('Xem bản scan'))),
        ])))),
      ])));
  }

  Widget _empty(Color muted, String title, String subtitle) => Padding(padding: const EdgeInsets.all(28), child: Column(children: [Icon(Icons.document_scanner_outlined, size: 42, color: muted), const SizedBox(height: 10), Text(title, style: const TextStyle(fontWeight: FontWeight.w600)), if (subtitle.isNotEmpty) Text(subtitle, textAlign: TextAlign.center, style: TextStyle(color: muted))]));
  String _date(String value) { final d = DateTime.tryParse(value)?.toLocal(); return d == null ? '—' : '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}'; }
}

class _Status extends StatelessWidget {
  final bool ready;
  final String status;
  const _Status({required this.ready, required this.status});
  @override
  Widget build(BuildContext context) { final color = ready ? Colors.green : Colors.orange; final label = ready ? (status == 'Ready' ? 'Sẵn sàng xem' : status) : 'Chờ cấp quyền'; return Container(padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4), decoration: BoxDecoration(color: color.withValues(alpha: .1), borderRadius: BorderRadius.circular(6)), child: Text(label, style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w600))); }
}
