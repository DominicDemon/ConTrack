import 'dart:typed_data';
import 'package:cross_file/cross_file.dart';
import 'package:cunning_document_scanner/cunning_document_scanner.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../../models/scans/signed_scan_dto.dart';
import '../../services/scans/customer_scan_api.dart';
import '../../utils/app_toast.dart';
import '../../utils/scan_pdf_builder.dart';
import 'scan_login_page.dart';

class CustomerContractScanCapturePage extends StatefulWidget {
  final SignedContractItem contract;
  const CustomerContractScanCapturePage({super.key, required this.contract});
  @override
  State<CustomerContractScanCapturePage> createState() => _CustomerContractScanCapturePageState();
}

class _CustomerContractScanCapturePageState extends State<CustomerContractScanCapturePage> {
  final List<Uint8List> _images = [];
  ScanFile? _pdf;
  bool _busy = false;
  String? _error;
  bool get _scannerAvailable => !kIsWeb && (defaultTargetPlatform == TargetPlatform.android || defaultTargetPlatform == TargetPlatform.iOS);

  Future<void> _scan() async {
    try {
      final paths = await CunningDocumentScanner.getPictures(noOfPages: 30, scannerSource: ScannerSource.camera);
      if (paths == null || paths.isEmpty) return;
      final images = await Future.wait(paths.map((path) => XFile(path).readAsBytes()));
      try { await CunningDocumentScanner.cleanCache(); } catch (_) {}
      if (mounted) setState(() { _pdf = null; _images.addAll(images); _error = null; });
    } on CunningDocumentScannerException catch (e) { if (mounted) AppToast.error(context, 'Không mở được trình quét: ${e.message}'); }
  }

  Future<void> _pick() async {
    final result = await FilePicker.pickFiles(type: FileType.custom, allowedExtensions: const ['pdf', 'jpg', 'jpeg', 'png'], withData: true);
    if (result == null || result.files.isEmpty || !mounted) return;
    final picked = result.files;
    final pdfs = picked.where((f) => f.extension?.toLowerCase() == 'pdf').toList();
    if (pdfs.isNotEmpty && picked.length != 1) { setState(() => _error = 'Chọn một PDF hoặc nhiều ảnh, không trộn hai loại.'); return; }
    if (pdfs.isNotEmpty) {
      final f = pdfs.single;
      setState(() { _images.clear(); _pdf = ScanFile(bytes: f.bytes!, fileName: f.name, contentType: 'application/pdf'); _error = null; });
    } else {
      final images = picked.map((f) => f.bytes!).toList();
      setState(() { _pdf = null; _images.addAll(images); _error = null; });
    }
  }

  Future<void> _upload() async {
    if (_pdf == null && _images.isEmpty) { setState(() => _error = 'Hãy chụp hoặc chọn bản scan.'); return; }
    setState(() { _busy = true; _error = null; });
    try {
      final file = _pdf ?? ScanFile(bytes: await compute(buildScanPdf, _images), fileName: 'scan-${widget.contract.documentCode ?? widget.contract.documentId}.pdf', contentType: 'application/pdf');
      if (file.bytes.length > ScanPdfBuilder.maxBytes) throw Exception('Tệp vượt quá giới hạn 20 MB.');
      final confirm = await showDialog<bool>(context: context, builder: (context) => AlertDialog(
        title: const Text('Lưu bản scan?'), content: const Text('Bản scan sẽ được lưu ngay vào hợp đồng của bạn.'),
        actions: [TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Hủy')), FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Lưu'))],
      ));
      if (confirm != true || !mounted) return;
      await CustomerScanApi.instance.upload(widget.contract.documentId, file: file);
      if (!mounted) return;
      AppToast.success(context, 'Đã lưu bản scan vào hợp đồng của bạn.');
      Navigator.pop(context, true);
    } on CustomerScanException catch (e) {
      if (e.isUnauthorized && mounted) await Navigator.push(context, MaterialPageRoute(builder: (_) => const ScanLoginPage(returnWhenDone: true)));
      else if (mounted) setState(() => _error = e.message);
    } catch (e) { if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', '')); }
    finally { if (mounted) setState(() => _busy = false); }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Bản scan hợp đồng')),
    body: ListView(padding: const EdgeInsets.all(16), children: [
      Text('${widget.contract.documentCode ?? 'Hợp đồng'} · ${widget.contract.documentName}', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 8), const Text('Tệp được lưu trực tiếp cho hợp đồng của bạn.'),
      const SizedBox(height: 16), Wrap(spacing: 8, children: [
        if (_scannerAvailable) FilledButton.tonalIcon(onPressed: _busy ? null : _scan, icon: const Icon(Icons.document_scanner), label: const Text('Quét bằng camera')),
        OutlinedButton.icon(onPressed: _busy ? null : _pick, icon: const Icon(Icons.attach_file), label: const Text('Chọn PDF / ảnh')),
      ]),
      if (_pdf != null) ListTile(title: Text(_pdf!.fileName), trailing: IconButton(onPressed: () => setState(() => _pdf = null), icon: const Icon(Icons.close))),
      if (_images.isNotEmpty) ListTile(title: Text('${_images.length} trang ảnh sẽ ghép thành PDF'), trailing: IconButton(onPressed: () => setState(_images.clear), icon: const Icon(Icons.close))),
      if (_error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
    ]),
    bottomNavigationBar: SafeArea(minimum: const EdgeInsets.all(16), child: FilledButton.icon(onPressed: _busy ? null : _upload, icon: _busy ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)) : const Icon(Icons.save), label: Text(_busy ? 'Đang lưu…' : 'Lưu bản scan'))),
  );
}
