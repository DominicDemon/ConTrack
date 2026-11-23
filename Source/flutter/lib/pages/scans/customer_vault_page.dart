import 'dart:async';
import 'dart:typed_data';
import 'package:cross_file/cross_file.dart';
import 'package:cunning_document_scanner/cunning_document_scanner.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../models/scans/signed_scan_dto.dart';
import '../../services/scans/customer_scan_api.dart';
import '../../utils/scan_pdf_builder.dart';

const _keywordLabels = {'ContractCode': 'Số HĐ', 'TaxCode': 'MST', 'PhoneNumber': 'SĐT', 'CustomerName': 'Tên', 'Email': 'Email'};
const _ocrLabels = {
  'QUEUED': 'Đang chờ đọc…',
  'PROCESSING': 'Đang đọc nội dung…',
  'FAILED': 'Không đọc được nội dung tệp.',
  'INTERRUPTED': 'Máy chủ khởi động lại khi đang đọc. Nhập key rồi bấm “Đọc lại”.',
};
const _trashDays = 30;

String _mb(int bytes) => '${(bytes / 1048576).toStringAsFixed(1).replaceAll('.', ',')} MB';
String _day(String iso) {
  final d = DateTime.tryParse(iso)?.toLocal();
  return d == null ? '—' : '${d.day.toString().padLeft(2, '0')}/${d.month.toString().padLeft(2, '0')}/${d.year}';
}

/// dd/mm/yyyy <-> yyyy-mm-dd for the API.
String? _isoDay(String text) {
  final m = RegExp(r'^(\d{2})/(\d{2})/(\d{4})$').firstMatch(text.trim());
  return m == null ? null : '${m[3]}-${m[2]}-${m[1]}';
}
String _viDay(String? iso) => iso == null ? '' : iso.split('-').reversed.join('/');

/// Types slashes in as dd/mm/yyyy is typed.
class _DayFormatter extends TextInputFormatter {
  @override
  TextEditingValue formatEditUpdate(TextEditingValue oldValue, TextEditingValue newValue) {
    final d = newValue.text.replaceAll(RegExp(r'\D'), '');
    final digits = d.length > 8 ? d.substring(0, 8) : d;
    final parts = [if (digits.isNotEmpty) digits.substring(0, digits.length.clamp(0, 2)), if (digits.length > 2) digits.substring(2, digits.length.clamp(2, 4)), if (digits.length > 4) digits.substring(4)];
    final text = parts.join('/');
    return TextEditingValue(text: text, selection: TextSelection.collapsed(offset: text.length));
  }
}

/// The customer's own document store (same as /portal/vault): scan and keep contracts and papers, also with other parties.
/// Files are encrypted for the customer's key; no employee can open them.
class CustomerVaultPage extends StatefulWidget {
  const CustomerVaultPage({super.key});
  @override
  State<CustomerVaultPage> createState() => _CustomerVaultPageState();
}

class _CustomerVaultPageState extends State<CustomerVaultPage> {
  final _api = CustomerScanApi.instance;
  final _search = TextEditingController();
  final _key = TextEditingController(text: RememberedFileKey.value);
  List<VaultItem>? _items;
  VaultUsage? _usage;
  List<String> _categories = [];
  String _folder = '';
  bool _trash = false, _showKey = false;
  bool? _hasKey;
  String? _busy, _error;
  Timer? _debounce, _poll;

  bool get _scannerAvailable => !kIsWeb && (defaultTargetPlatform == TargetPlatform.android || defaultTargetPlatform == TargetPlatform.iOS);

  @override
  void initState() {
    super.initState();
    _api.hasKey().then((v) { if (mounted) setState(() => _hasKey = v); }).catchError((_) {});
    _load();
  }

  @override
  void dispose() { _search.dispose(); _key.dispose(); _debounce?.cancel(); _poll?.cancel(); super.dispose(); }

  Future<void> _load() async {
    try {
      final result = await Future.wait([_api.vault(query: _search.text, category: _folder, trash: _trash), _api.vaultUsage(), _api.vaultCategories()]);
      if (!mounted) return;
      setState(() { _items = result[0] as List<VaultItem>; _usage = result[1] as VaultUsage; _categories = result[2] as List<String>; _error = null; });
      // Documents still being read refresh by themselves.
      final reading = _items!.any((i) => i.ocrStatus == 'QUEUED' || i.ocrStatus == 'PROCESSING');
      _poll?.cancel();
      if (reading) _poll = Timer(const Duration(seconds: 4), _load);
    } on CustomerScanException catch (e) { _failed(e); }
  }

  void _failed(CustomerScanException e) {
    if (!mounted) return;
    if (e.isUnauthorized) { Navigator.of(context).pushNamedAndRemoveUntil('/scans/login', (route) => route.isFirst); return; }
    setState(() => _error = e.message);
  }

  Future<void> _run(String id, Future<void> Function() action, {String? done}) async {
    setState(() { _busy = id; _error = null; });
    try {
      await action();
      if (done != null && mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(done)));
      await _load();
    } on CustomerScanException catch (e) { _failed(e); }
    catch (e) { if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', '')); }
    finally { if (mounted) setState(() => _busy = null); }
  }

  String? _needKey() {
    final key = _key.text.trim();
    if (key.isEmpty) { setState(() => _error = 'Nhập key xem bản scan của bạn trước.'); return null; }
    return key;
  }

  // ---- Adding documents -------------------------------------------------------------------------------------------

  Future<void> _add() async {
    final files = <ScanFile>[];
    final images = <Uint8List>[];
    final title = TextEditingController(), note = TextEditingController(), expires = TextEditingController();
    var category = '';
    String? error;
    final saved = await showModalBottomSheet<bool>(context: context, isScrollControlled: true, useSafeArea: true, builder: (context) => StatefulBuilder(builder: (context, setSheet) {
      Future<void> scan() async {
        try {
          final paths = await CunningDocumentScanner.getPictures(noOfPages: 30, scannerSource: ScannerSource.camera);
          if (paths == null || paths.isEmpty) return;
          final bytes = await Future.wait(paths.map((p) => XFile(p).readAsBytes()));
          try { await CunningDocumentScanner.cleanCache(); } catch (_) {}
          setSheet(() { images.addAll(bytes); error = null; });
        } on CunningDocumentScannerException catch (e) { setSheet(() => error = 'Không mở được trình quét: ${e.message}'); }
      }
      Future<void> pick() async {
        final picked = await FilePicker.pickFiles(type: FileType.custom, allowedExtensions: const ['pdf', 'jpg', 'jpeg', 'png']);
        if (picked.isEmpty) return;
        final read = await Future.wait(picked.map((f) => f.readAsBytes()));
        setSheet(() {
          for (final (i, f) in picked.indexed) {
            if (f.extension?.toLowerCase() == 'pdf') { files.add(ScanFile(bytes: read[i], fileName: f.name, contentType: 'application/pdf')); }
            else { images.add(read[i]); }
          }
          if (title.text.isEmpty && files.length == 1 && images.isEmpty) title.text = files.single.fileName.replaceAll(RegExp(r'\.pdf$', caseSensitive: false), '');
          error = null;
        });
      }
      final count = files.length + (images.isEmpty ? 0 : 1);
      return Padding(padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + MediaQuery.of(context).viewInsets.bottom), child: SingleChildScrollView(child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, mainAxisSize: MainAxisSize.min, children: [
        Text('Thêm tài liệu vào kho', style: Theme.of(context).textTheme.titleMedium),
        const SizedBox(height: 12),
        Wrap(spacing: 8, runSpacing: 8, children: [
          if (_scannerAvailable) FilledButton.tonalIcon(onPressed: scan, icon: const Icon(Icons.document_scanner), label: const Text('Quét bằng camera')),
          OutlinedButton.icon(onPressed: pick, icon: const Icon(Icons.attach_file), label: const Text('Chọn PDF / ảnh (nhiều tệp)')),
        ]),
        if (count > 0) ListTile(contentPadding: EdgeInsets.zero, title: Text('Sẽ lưu $count tài liệu'),
          subtitle: Text([...files.map((f) => f.fileName), if (images.isNotEmpty) '${images.length} trang ảnh ghép thành 1 PDF'].join('\n')),
          trailing: IconButton(onPressed: () => setSheet(() { files.clear(); images.clear(); }), icon: const Icon(Icons.close))),
        const SizedBox(height: 8),
        TextField(controller: title, maxLength: 300, decoration: InputDecoration(labelText: count > 1 ? 'Tên tài liệu (để trống: theo tên tệp)' : 'Tên tài liệu', border: const OutlineInputBorder(), isDense: true)),
        Autocomplete<String>(optionsBuilder: (v) => _categories.where((c) => c.toLowerCase().contains(v.text.toLowerCase())), onSelected: (v) => category = v,
          fieldViewBuilder: (context, controller, focus, _) => TextField(controller: controller, focusNode: focus, maxLength: 100, onChanged: (v) => category = v,
            decoration: const InputDecoration(labelText: 'Thư mục (chọn hoặc gõ tên mới)', border: OutlineInputBorder(), isDense: true))),
        TextField(controller: expires, keyboardType: TextInputType.number, inputFormatters: [_DayFormatter()],
          decoration: const InputDecoration(labelText: 'Ngày hết hạn (nếu có)', hintText: 'dd/mm/yyyy', border: OutlineInputBorder(), isDense: true)),
        const SizedBox(height: 12),
        TextField(controller: note, maxLength: 1000, decoration: const InputDecoration(labelText: 'Ghi chú / từ khóa (không bắt buộc)', border: OutlineInputBorder(), isDense: true)),
        if (error != null) Padding(padding: const EdgeInsets.only(bottom: 8), child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
        FilledButton.icon(onPressed: count == 0 ? null : () {
          if (expires.text.isNotEmpty && _isoDay(expires.text) == null) { setSheet(() => error = 'Ngày hết hạn theo dạng dd/mm/yyyy.'); return; }
          Navigator.pop(context, true);
        }, icon: const Icon(Icons.save), label: const Text('Lưu vào kho')),
      ])));
    }));
    if (saved != true || !mounted) { title.dispose(); note.dispose(); expires.dispose(); return; }

    final count = files.length + (images.isEmpty ? 0 : 1);
    final failed = <String>[];
    var done = 0;
    setState(() { _busy = 'upload'; _error = null; });
    try {
      if (images.isNotEmpty) {
        setState(() => _busy = 'upload:Đang ghép ảnh thành PDF…');
        final name = title.text.trim().isEmpty ? 'tai-lieu' : title.text.trim();
        files.add(ScanFile(bytes: await compute(buildScanPdf, images), fileName: '${name.replaceAll(RegExp(r'[^\p{L}\p{N}_-]+', unicode: true), '-')}.pdf', contentType: 'application/pdf'));
      }
      for (final (index, file) in files.indexed) {
        setState(() => _busy = 'upload:Đang lưu ${index + 1}/${files.length}…');
        final isPhotoPdf = images.isNotEmpty && index == files.length - 1;
        final name = count == 1 && title.text.trim().isNotEmpty ? title.text.trim()
            : isPhotoPdf ? (title.text.trim().isEmpty ? 'Tài liệu chụp' : title.text.trim()) : file.fileName.replaceAll(RegExp(r'\.pdf$', caseSensitive: false), '');
        try {
          if (file.bytes.length > ScanPdfBuilder.maxBytes) throw CustomerScanException('“${file.fileName}” vượt 20 MB.');
          await _api.vaultUpload(file, title: name, note: note.text, category: category, expiresOn: _isoDay(expires.text));
          done++;
        } on CustomerScanException catch (e) {
          if (e.isUnauthorized) { _failed(e); return; }
          failed.add(e.message);
        }
      }
    } catch (e) { failed.add(e.toString()); }
    finally {
      title.dispose(); note.dispose(); expires.dispose();
      if (mounted) {
        setState(() { _busy = null; _error = failed.isEmpty ? null : failed.join('\n'); });
        if (done > 0) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Đã lưu $done tài liệu. Hệ thống đang đọc nội dung để tìm kiếm.')));
        await _load();
      }
    }
  }

  // ---- Actions on a document --------------------------------------------------------------------------------------

  Future<void> _view(VaultItem item) async {
    final key = _needKey(); if (key == null) return;
    setState(() { _busy = item.id; _error = null; });
    try {
      final response = await _api.vaultOpen(item.id, key);
      if (!mounted) return;
      if (item.contentType.startsWith('image/')) {
        await showDialog<void>(context: context, builder: (context) => Dialog.fullscreen(child: Scaffold(
          appBar: AppBar(title: Text(item.title), leading: IconButton(onPressed: () => Navigator.pop(context), icon: const Icon(Icons.close))),
          body: InteractiveViewer(maxScale: 6, child: Center(child: Image.memory(Uint8List.fromList(response.bodyBytes)))))));
      } else {
        final path = await FilePicker.saveFile(fileName: item.fileName, bytes: response.bodyBytes, mimeType: item.contentType);
        if (mounted && path != null) ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Đã lưu tệp về thiết bị.')));
      }
    } on CustomerScanException catch (e) { _failed(e); }
    finally { if (mounted) setState(() => _busy = null); }
  }

  Future<void> _edit(VaultItem item) async {
    final title = TextEditingController(text: item.title), note = TextEditingController(text: item.note ?? ''),
        category = TextEditingController(text: item.category ?? ''), expires = TextEditingController(text: _viDay(item.expiresOn)),
        remind = TextEditingController(text: '${item.remindDays ?? 30}');
    String? error;
    final ok = await showDialog<bool>(context: context, builder: (context) => StatefulBuilder(builder: (context, setDialog) => AlertDialog(
      title: const Text('Sửa thông tin tài liệu'),
      content: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
        TextField(controller: title, maxLength: 300, decoration: const InputDecoration(labelText: 'Tên tài liệu')),
        TextField(controller: category, maxLength: 100, decoration: InputDecoration(labelText: 'Thư mục', suffixIcon: PopupMenuButton<String>(icon: const Icon(Icons.arrow_drop_down),
          onSelected: (v) => category.text = v, itemBuilder: (_) => _categories.map((c) => PopupMenuItem(value: c, child: Text(c))).toList()))),
        TextField(controller: note, maxLength: 1000, decoration: const InputDecoration(labelText: 'Ghi chú / từ khóa')),
        TextField(controller: expires, keyboardType: TextInputType.number, inputFormatters: [_DayFormatter()], onChanged: (_) => setDialog(() {}),
          decoration: const InputDecoration(labelText: 'Ngày hết hạn', hintText: 'dd/mm/yyyy')),
        TextField(controller: remind, enabled: _isoDay(expires.text) != null, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Email nhắc trước (ngày)')),
        const SizedBox(height: 8),
        const Text('Có ngày hết hạn: hệ thống gửi email nhắc theo số ngày trên, rồi 7 ngày trước và đúng ngày hết hạn.', style: TextStyle(fontSize: 12)),
        if (error != null) Padding(padding: const EdgeInsets.only(top: 8), child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
      ])),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')),
        FilledButton(onPressed: () {
          if (expires.text.isNotEmpty && _isoDay(expires.text) == null) { setDialog(() => error = 'Ngày hết hạn theo dạng dd/mm/yyyy.'); return; }
          Navigator.pop(context, true);
        }, child: const Text('Lưu')),
      ])));
    if (ok == true) {
      final iso = _isoDay(expires.text);
      await _run(item.id, () => _api.vaultEdit(item.id, title: title.text, note: note.text, category: category.text, expiresOn: iso,
          remindDays: iso == null ? null : int.tryParse(remind.text)), done: 'Đã lưu thông tin tài liệu.');
    }
    for (final c in [title, note, category, expires, remind]) { c.dispose(); }
  }

  Future<void> _addKeyword(VaultItem item) async {
    var type = 'ContractCode';
    final value = TextEditingController();
    final ok = await showDialog<bool>(context: context, builder: (context) => StatefulBuilder(builder: (context, setDialog) => AlertDialog(
      title: const Text('Thêm thông tin để tìm'),
      content: Column(mainAxisSize: MainAxisSize.min, children: [
        DropdownButtonFormField<String>(initialValue: type, decoration: const InputDecoration(labelText: 'Loại'),
          items: _keywordLabels.entries.map((e) => DropdownMenuItem(value: e.key, child: Text(e.value))).toList(), onChanged: (v) => setDialog(() => type = v ?? type)),
        TextField(controller: value, autofocus: true, maxLength: 120, decoration: const InputDecoration(labelText: 'Giá trị', hintText: 'VD: HDTVP-2026-0099')),
      ]),
      actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Thêm'))])));
    if (ok == true && value.text.trim().isNotEmpty) await _run(item.id, () => _api.vaultAddKeyword(item.id, type, value.text.trim()));
    value.dispose();
  }

  Future<void> _email(VaultItem item) async {
    final key = _needKey(); if (key == null) return;
    final to = TextEditingController(), note = TextEditingController();
    final ok = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
      title: Text('Gửi “${item.title}”', maxLines: 2, overflow: TextOverflow.ellipsis),
      content: Column(mainAxisSize: MainAxisSize.min, children: [
        TextField(controller: to, keyboardType: TextInputType.emailAddress, decoration: const InputDecoration(labelText: 'Email người nhận', hintText: 'cách nhau dấu phẩy')),
        TextField(controller: note, maxLines: 3, maxLength: 2000, decoration: const InputDecoration(labelText: 'Lời nhắn (không bắt buộc)')),
        const Text('Tệp gửi đi không còn được mã hóa. Mỗi giờ gửi tối đa 20 địa chỉ.', style: TextStyle(fontSize: 12)),
      ]),
      actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Gửi'))]));
    if (ok == true) {
      final recipients = to.text.split(RegExp(r'[,;\s]+')).where((e) => e.isNotEmpty).toList();
      final note0 = note.text.trim();
      List<String> sent = [];
      await _run(item.id, () async { sent = await _api.vaultEmail(item.id, key, recipients, note0.isEmpty ? null : note0); });
      if (mounted && sent.isNotEmpty) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('Đã gửi tới ${sent.join(', ')}.')));
    }
    to.dispose(); note.dispose();
  }

  Future<bool> _confirm(String title, String text, String action) async => await showDialog<bool>(context: context, builder: (context) => AlertDialog(
    title: Text(title), content: Text(text),
    actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(action))])) == true;

  Future<void> _action(VaultItem item, String action) async {
    switch (action) {
      case 'email': await _email(item);
      case 'edit': await _edit(item);
      case 'keyword': await _addKeyword(item);
      case 'reread':
        final key = _needKey(); if (key == null) return;
        await _run(item.id, () => _api.vaultReread(item.id, key), done: 'Đang đọc lại nội dung. Thông tin bạn tự thêm được giữ nguyên.');
      case 'trash':
        if (await _confirm('Chuyển vào thùng rác?', '“${item.title}” sẽ ở thùng rác $_trashDays ngày, bạn khôi phục được trong thời gian đó.', 'Chuyển')) {
          await _run(item.id, () => _api.vaultTrash(item.id), done: 'Đã chuyển vào thùng rác.');
        }
      case 'restore': await _run(item.id, () => _api.vaultRestore(item.id), done: 'Đã khôi phục “${item.title}”.');
      case 'purge':
        if (await _confirm('Xóa hẳn?', '“${item.title}” sẽ bị xóa hẳn, không khôi phục được.', 'Xóa hẳn')) {
          await _run(item.id, () => _api.vaultPurge(item.id), done: 'Đã xóa hẳn tài liệu.');
        }
    }
  }

  // ---- UI ---------------------------------------------------------------------------------------------------------

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context), muted = theme.colorScheme.onSurface.withValues(alpha: .62);
    final usage = _usage;
    final uploading = _busy?.startsWith('upload') == true;
    return Scaffold(
      appBar: AppBar(title: const Text('Kho lưu trữ riêng'), actions: [IconButton(onPressed: _load, tooltip: 'Tải lại', icon: const Icon(Icons.refresh))]),
      floatingActionButton: _hasKey == true && !_trash ? FloatingActionButton.extended(onPressed: _busy == null ? _add : null,
        icon: uploading ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.add),
        label: Text(uploading && _busy!.length > 7 ? _busy!.substring(7) : 'Thêm tài liệu')) : null,
      body: RefreshIndicator(onRefresh: _load, child: ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.fromLTRB(16, 16, 16, 96), children: [
        Text('Scan và lưu hợp đồng, tài liệu của bạn, kể cả hợp đồng với đối tác khác. Tài liệu được mã hóa bằng key của bạn; nhân viên công ty không xem được.', style: TextStyle(color: muted)),
        ExpansionTile(tilePadding: EdgeInsets.zero, title: Text('Ai có thể mở tài liệu của tôi?', style: TextStyle(fontSize: 13, color: theme.colorScheme.primary)), children: [
          for (final line in const [
            'Chỉ bạn, bằng key bạn tự đặt. Không nhân viên nào được cấp quyền mở kho lưu trữ riêng.',
            'Để bạn không mất tài liệu khi quên key, hệ thống giữ thêm một khóa khôi phục do quản trị hệ thống quản lý. Khóa này chỉ được dùng khi chính bạn đặt lại key (cần mật khẩu và mã gửi qua email).',
            'Mỗi lần khóa khôi phục được dùng, hệ thống ghi nhật ký và gửi email báo cho bạn.',
            'Tài liệu bạn gửi qua email cho người khác không còn được mã hóa.',
          ]) Padding(padding: const EdgeInsets.only(bottom: 6), child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [Text('•  ', style: TextStyle(color: muted)), Expanded(child: Text(line, style: TextStyle(fontSize: 12, color: muted)))])),
        ]),
        if (usage != null) ...[
          Text('Đã dùng ${_mb(usage.usedBytes)} / 1 GB · ${usage.files} tài liệu${usage.inTrash > 0 ? ' · ${usage.inTrash} trong thùng rác' : ''}', style: TextStyle(fontSize: 12, color: muted)),
          const SizedBox(height: 4),
          LinearProgressIndicator(value: (usage.usedBytes / usage.limitBytes).clamp(0, 1).toDouble(), minHeight: 6, borderRadius: BorderRadius.circular(3)),
        ],
        const SizedBox(height: 12),
        if (_hasKey == false) Card(child: ListTile(leading: const Icon(Icons.key_outlined), title: const Text('Bạn cần tạo key xem bản scan trước'),
          subtitle: const Text('Tài liệu được mã hóa bằng key đó. Tạo key ở màn “Bản scan hợp đồng”.'), onTap: () => Navigator.pop(context))),
        if (_hasKey == true && !_trash) Card(child: Padding(padding: const EdgeInsets.all(12), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          TextField(controller: _key, obscureText: !_showKey, autocorrect: false, enableSuggestions: false, onChanged: (_) => setState(() {}),
            decoration: InputDecoration(labelText: 'Key để xem / gửi / đọc lại', isDense: true, border: const OutlineInputBorder(),
              suffixIcon: IconButton(onPressed: () => setState(() => _showKey = !_showKey), icon: Icon(_showKey ? Icons.visibility_off : Icons.visibility)))),
          if (RememberedFileKey.value.isNotEmpty) Row(children: [
            Expanded(child: Text('Key được nhớ trên máy này ${RememberedFileKey.minutes} phút kể từ lần dùng cuối.', style: TextStyle(fontSize: 11, color: muted))),
            TextButton(onPressed: () => setState(() { RememberedFileKey.forget(); _key.clear(); }), child: const Text('Quên key ngay', style: TextStyle(fontSize: 12))),
          ]),
        ]))),
        const SizedBox(height: 8),
        SegmentedButton<bool>(showSelectedIcon: false, segments: [
          const ButtonSegment(value: false, icon: Icon(Icons.folder_outlined), label: Text('Tài liệu')),
          ButtonSegment(value: true, icon: const Icon(Icons.delete_outline), label: Text('Thùng rác${(usage?.inTrash ?? 0) > 0 ? ' (${usage!.inTrash})' : ''}')),
        ], selected: {_trash}, onSelectionChanged: (v) { setState(() { _trash = v.first; _items = null; }); _load(); }),
        if (_trash) Padding(padding: const EdgeInsets.only(top: 8), child: Text('Tài liệu trong thùng rác vẫn tính vào dung lượng và tự xóa hẳn sau $_trashDays ngày.', style: TextStyle(fontSize: 12, color: muted))),
        const SizedBox(height: 12),
        TextField(controller: _search, onChanged: (_) { _debounce?.cancel(); _debounce = Timer(const Duration(milliseconds: 350), _load); },
          decoration: const InputDecoration(prefixIcon: Icon(Icons.search), hintText: 'Tên, ghi chú, số HĐ, MST, SĐT, email', border: OutlineInputBorder(), isDense: true)),
        const SizedBox(height: 8),
        SingleChildScrollView(scrollDirection: Axis.horizontal, child: Row(children: [
          for (final folder in ['', ..._categories]) Padding(padding: const EdgeInsets.only(right: 6), child: ChoiceChip(label: Text(folder.isEmpty ? 'Tất cả' : folder), selected: _folder == folder,
            onSelected: (_) { setState(() => _folder = folder); _load(); })),
        ])),
        if (_error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(_error!, style: TextStyle(color: theme.colorScheme.error))),
        const SizedBox(height: 12),
        if (_items == null && _error == null) const Padding(padding: EdgeInsets.all(36), child: Center(child: CircularProgressIndicator())),
        if (_items?.isEmpty == true) Padding(padding: const EdgeInsets.all(28), child: Column(children: [
          Icon(_trash ? Icons.delete_outline : Icons.inventory_2_outlined, size: 42, color: muted), const SizedBox(height: 10),
          Text(_search.text.isNotEmpty || _folder.isNotEmpty ? 'Không tìm thấy tài liệu phù hợp' : _trash ? 'Thùng rác trống' : 'Kho của bạn chưa có tài liệu', style: const TextStyle(fontWeight: FontWeight.w600)),
        ])),
        ...?_items?.map((item) => _card(item, theme, muted)),
      ])),
    );
  }

  Widget _card(VaultItem item, ThemeData theme, Color muted) {
    final busy = _busy == item.id;
    final warn = item.ocrStatus == 'INTERRUPTED' || item.ocrStatus == 'FAILED';
    final days = item.daysLeft;
    final expiryColor = days == null ? muted : days < 0 ? Colors.red.shade700 : days <= (item.remindDays ?? 30) ? Colors.orange.shade800 : muted;
    final left = item.trashedAt == null ? null : _trashDays - DateTime.now().difference(DateTime.parse(item.trashedAt!)).inDays;
    return Card(margin: const EdgeInsets.only(bottom: 10), child: Padding(padding: const EdgeInsets.fromLTRB(14, 10, 4, 12), child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Padding(padding: const EdgeInsets.only(top: 4), child: Icon(item.contentType.startsWith('image/') ? Icons.image_outlined : Icons.picture_as_pdf_outlined, color: theme.colorScheme.primary)),
        const SizedBox(width: 10),
        Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(item.title, style: theme.textTheme.titleSmall),
          Text([if (item.category != null) item.category!, _mb(item.sizeBytes), if (item.pageCount != null) '${item.pageCount} trang', _trash ? 'xóa ${_day(item.trashedAt!)}' : _day(item.uploadedAt)].join(' · '),
            style: TextStyle(fontSize: 12, color: muted)),
        ])),
        if (busy) const Padding(padding: EdgeInsets.all(12), child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)))
        else PopupMenuButton<String>(enabled: _busy == null, onSelected: (a) => _action(item, a), itemBuilder: (_) => _trash
          ? const [PopupMenuItem(value: 'restore', child: ListTile(leading: Icon(Icons.restore), title: Text('Khôi phục'))),
                   PopupMenuItem(value: 'purge', child: ListTile(leading: Icon(Icons.delete_forever_outlined), title: Text('Xóa hẳn')))]
          : [const PopupMenuItem(value: 'email', child: ListTile(leading: Icon(Icons.mail_outline), title: Text('Gửi email'))),
             const PopupMenuItem(value: 'edit', child: ListTile(leading: Icon(Icons.edit_outlined), title: Text('Sửa tên, thư mục, hạn'))),
             const PopupMenuItem(value: 'keyword', child: ListTile(leading: Icon(Icons.add), title: Text('Thêm số HĐ, MST, SĐT…'))),
             if (item.ocrStatus != 'QUEUED' && item.ocrStatus != 'PROCESSING') const PopupMenuItem(value: 'reread', child: ListTile(leading: Icon(Icons.refresh), title: Text('Đọc lại nội dung'))),
             const PopupMenuItem(value: 'trash', child: ListTile(leading: Icon(Icons.delete_outline), title: Text('Chuyển vào thùng rác')))]),
      ]),
      if (item.note != null) Padding(padding: const EdgeInsets.only(top: 4, right: 10), child: Text(item.note!, style: TextStyle(fontSize: 12, color: muted))),
      if (item.expiresOn != null && days != null) Padding(padding: const EdgeInsets.only(top: 4), child: Row(children: [
        if (item.remindDays != null) Icon(Icons.notifications_active_outlined, size: 13, color: expiryColor),
        const SizedBox(width: 4),
        Text(days < 0 ? 'Đã hết hạn ${_viDay(item.expiresOn)}' : days == 0 ? 'Hết hạn hôm nay' : 'Hết hạn ${_viDay(item.expiresOn)} · còn $days ngày', style: TextStyle(fontSize: 12, color: expiryColor)),
      ])),
      if (item.sameContractCode > 0) Padding(padding: const EdgeInsets.only(top: 4), child: Text('Có thể trùng: cùng số hợp đồng với ${item.sameContractCode} tài liệu khác', style: TextStyle(fontSize: 12, color: Colors.orange.shade800))),
      if (item.ocrStatus != 'SUCCESS') Padding(padding: const EdgeInsets.only(top: 4, right: 10), child: Text(_ocrLabels[item.ocrStatus] ?? item.ocrStatus, style: TextStyle(fontSize: 12, color: warn ? Colors.orange.shade800 : muted))),
      if (item.keywords.isNotEmpty) Padding(padding: const EdgeInsets.only(top: 6, right: 10), child: Wrap(spacing: 6, runSpacing: 6, children: [
        for (final k in item.keywords) InputChip(
          visualDensity: VisualDensity.compact, materialTapTargetSize: MaterialTapTargetSize.shrinkWrap,
          backgroundColor: k.manual ? theme.colorScheme.secondaryContainer : null, tooltip: k.manual ? 'Bạn tự thêm' : 'Đọc từ tài liệu',
          label: Text('${_keywordLabels[k.type] ?? k.type}: ${k.value}', style: const TextStyle(fontSize: 12)),
          onDeleted: _trash || _busy != null ? null : () => _run(item.id, () => _api.vaultRemoveKeyword(item.id, k.id))),
      ])),
      if (item.ocrStatus == 'SUCCESS' && item.keywords.isEmpty) Padding(padding: const EdgeInsets.only(top: 4), child: Text('Không đọc được từ khóa', style: TextStyle(fontSize: 12, color: muted))),
      if (left != null) Padding(padding: const EdgeInsets.only(top: 4), child: Text('Tự xóa hẳn sau ${left.clamp(0, _trashDays)} ngày', style: TextStyle(fontSize: 12, color: muted))),
      if (!_trash) Align(alignment: Alignment.centerRight, child: TextButton.icon(onPressed: _busy == null ? () => _view(item) : null,
        icon: const Icon(Icons.visibility_outlined), label: Text(item.contentType.startsWith('image/') ? 'Xem' : 'Xem / tải về'))),
    ])));
  }
}
