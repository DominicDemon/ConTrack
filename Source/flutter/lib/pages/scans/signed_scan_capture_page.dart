import 'package:cross_file/cross_file.dart';
import 'package:cunning_document_scanner/cunning_document_scanner.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import '../../models/scans/signed_scan_dto.dart';
import '../../services/scans/internal_api_client.dart';
import '../../services/scans/signed_scan_api.dart';
import '../../theme/app_colors.dart';
import '../../utils/app_format_utils.dart';
import '../../utils/app_toast.dart';
import '../../utils/scan_pdf_builder.dart';
import 'scan_login_page.dart';

/// Scans the signed paper copy and uploads it. Pops the refreshed overview once saved.
class SignedScanCapturePage extends StatefulWidget {
  final SignedContractOverview overview;

  const SignedScanCapturePage({super.key, required this.overview});

  @override
  State<SignedScanCapturePage> createState() => _SignedScanCapturePageState();
}

class _SignedScanCapturePageState extends State<SignedScanCapturePage> {
  static const int _maxScannerPages = 30;

  /// Each page as an image (camera or picked), joined into one PDF when uploading.
  final List<Uint8List> _pageImages = [];

  /// A picked PDF is sent as it is.
  ScanFile? _pickedPdf;

  /// The PDF built from [_pageImages], kept so a retry after a network error is instant.
  ScanFile? _scanFile;

  DateTime? _signDate;
  bool _isBuilding = false;
  bool _isSubmitting = false;
  String? _fileError;
  String? _dateError;

  bool get _hasScan => _pageImages.isNotEmpty || _pickedPdf != null;
  bool get _isBusy => _isBuilding || _isSubmitting;

  /// The document scanner exists on Android and iOS only; elsewhere files are picked.
  bool get _scannerAvailable =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android || defaultTargetPlatform == TargetPlatform.iOS);

  void _pagesChanged() {
    _scanFile = null;
    _fileError = null;
  }

  Future<void> _scanWithCamera() async {
    try {
      final paths = await CunningDocumentScanner.getPictures(
        noOfPages: _maxScannerPages,
        scannerSource: ScannerSource.camera,
      );
      if (paths == null || paths.isEmpty) return;

      final images = await Future.wait(paths.map((path) => XFile(path).readAsBytes()));
      try {
        await CunningDocumentScanner.cleanCache();
      } catch (_) {}
      if (!mounted) return;
      setState(() {
        _pickedPdf = null;
        _pageImages.addAll(images);
        _pagesChanged();
      });
    } on CunningDocumentScannerException catch (error) {
      if (!mounted) return;
      AppToast.error(
        context,
        error.code == 'permission_denied'
            ? 'Cần cấp quyền camera để quét bản ký.'
            : 'Không mở được trình quét: ${error.message}',
      );
    }
  }

  Future<void> _pickFiles() async {
    final files = await FilePicker.pickFiles(
      dialogTitle: 'Chọn bản scan đã ký',
      type: FileType.custom,
      allowedExtensions: const ['pdf', 'jpg', 'jpeg', 'png'],
    );
    if (files.isEmpty || !mounted) return;

    final pdfs = files.where((file) => file.extension?.toLowerCase() == 'pdf').toList();
    if (pdfs.isNotEmpty) {
      if (files.length > 1) {
        AppToast.error(context, 'Chọn một tệp PDF, hoặc nhiều ảnh (không trộn PDF với ảnh).');
        return;
      }
      final bytes = await pdfs.first.readAsBytes();
      if (!mounted) return;
      setState(() {
        _pageImages.clear();
        _pickedPdf = ScanFile(bytes: bytes, fileName: pdfs.first.name, contentType: 'application/pdf');
        _pagesChanged();
      });
      return;
    }

    final images = await Future.wait(files.map((file) => file.readAsBytes()));
    if (!mounted) return;
    setState(() {
      _pickedPdf = null;
      _pageImages.addAll(images);
      _pagesChanged();
    });
  }

  void _removePage(int index) {
    setState(() {
      _pageImages.removeAt(index);
      _pagesChanged();
    });
  }

  void _removePickedPdf() {
    setState(() {
      _pickedPdf = null;
      _pagesChanged();
    });
  }

  Future<void> _pickSignDate() async {
    final today = DateUtils.dateOnly(DateTime.now());
    final picked = await showDatePicker(
      context: context,
      initialDate: _signDate ?? today,
      firstDate: DateTime(2000),
      lastDate: today,
      helpText: 'Chọn ngày ký',
      cancelText: 'Hủy',
      confirmText: 'Chọn',
    );
    if (picked != null) {
      setState(() {
        _signDate = picked;
        _dateError = null;
      });
    }
  }

  Future<ScanFile?> _ensureScanFile() async {
    if (_scanFile != null) return _scanFile;
    if (_pickedPdf != null) return _scanFile = _pickedPdf;

    setState(() => _isBuilding = true);
    try {
      final pdf = await compute(buildScanPdf, List<Uint8List>.of(_pageImages));
      return _scanFile = ScanFile(bytes: pdf, fileName: _pdfFileName(), contentType: 'application/pdf');
    } on ScanTooLargeException catch (error) {
      if (mounted) setState(() => _fileError = error.toString());
    } on FormatException catch (error) {
      if (mounted) setState(() => _fileError = error.message);
    } finally {
      if (mounted) setState(() => _isBuilding = false);
    }
    return null;
  }

  String _pdfFileName() {
    final code = widget.overview.documentCode ?? 'hop-dong-${widget.overview.documentId}';
    return 'ban-ky-${code.replaceAll(RegExp(r'[^A-Za-z0-9_-]+'), '-')}.pdf';
  }

  Future<void> _submit() async {
    setState(() {
      _fileError = _hasScan ? null : 'Hãy chụp các trang hoặc chọn tệp PDF bản ký.';
      _dateError = _signDate == null ? 'Hãy chọn ngày ký ghi trên tài liệu.' : null;
    });
    if (_fileError != null || _dateError != null) return;

    final confirmed = await _confirmUpload();
    if (confirmed != true || !mounted) return;

    final file = await _ensureScanFile();
    if (file == null || !mounted) return;
    if (file.bytes.length > ScanPdfBuilder.maxBytes) {
      setState(() => _fileError = 'Tệp vượt quá giới hạn 20 MB của máy chủ. Hãy scan thành PDF nhẹ hơn hoặc chụp ít trang hơn.');
      return;
    }

    setState(() => _isSubmitting = true);
    try {
      final updated = await SignedScanApi.upload(
        widget.overview.documentId,
        versionId: widget.overview.currentVersion!.versionId,
        signDate: _signDate!,
        file: file,
      );
      if (!mounted) return;
      AppToast.success(context, 'Đã tải bản ký lên. Máy chủ đang đọc bản scan.');
      Navigator.of(context).pop(updated);
    } on InternalApiException catch (error) {
      if (!mounted) return;
      if (error.isUnauthorized) {
        await _signInAgain();
      } else {
        AppToast.error(context, error.message);
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  /// The pages stay on this screen while the employee signs in again.
  Future<void> _signInAgain() async {
    AppToast.error(context, 'Phiên đăng nhập đã hết hạn. Đăng nhập lại để tải bản ký lên.');
    final signedIn = await Navigator.of(context).push<bool>(
      MaterialPageRoute(builder: (_) => const ScanLoginPage(returnWhenDone: true)),
    );
    if (signedIn == true && mounted) {
      AppToast.show(context, 'Đã đăng nhập lại. Bấm tải lên lần nữa.');
    }
  }

  Future<bool?> _confirmUpload() {
    final version = widget.overview.currentVersion!;
    return showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Tải bản ký lên?', style: TextStyle(fontWeight: FontWeight.bold)),
        content: Text(
          'Bản ký được lưu cho phiên bản ${version.versionNo}, ngày ký ${AppFormatUtils.formatDate(_signDate)}. '
          'Sau khi tải lên, bản ký chờ người có quyền kiểm tra trên web.',
        ),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Hủy')),
          FilledButton(onPressed: () => Navigator.of(context).pop(true), child: const Text('Tải lên')),
        ],
      ),
    );
  }

  Future<void> _confirmLeave() async {
    final leave = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Bỏ bản scan?', style: TextStyle(fontWeight: FontWeight.bold)),
        content: const Text('Các trang đã quét chưa được tải lên sẽ mất.'),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: const Text('Ở lại')),
          FilledButton(onPressed: () => Navigator.of(context).pop(true), child: const Text('Bỏ')),
        ],
      ),
    );
    if (leave == true && mounted) Navigator.of(context).pop();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final overview = widget.overview;

    return PopScope(
      canPop: !_hasScan,
      onPopInvokedWithResult: (didPop, _) {
        if (!didPop && !_isSubmitting) _confirmLeave();
      },
      child: Scaffold(
        backgroundColor: theme.colorScheme.surface,
        appBar: AppBar(title: const Text('Tải bản ký lên'), scrolledUnderElevation: 0),
        body: ListView(
          padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
          children: [
            Text('${overview.documentCode ?? ''} · ${overview.documentName}', style: theme.textTheme.titleSmall),
            const SizedBox(height: 16),
            _buildScanStep(theme),
            const SizedBox(height: 24),
            _buildSignDateStep(theme),
          ],
        ),
        bottomNavigationBar: SafeArea(
          minimum: const EdgeInsets.fromLTRB(16, 8, 16, 16),
          child: FilledButton.icon(
            onPressed: _isBusy ? null : _submit,
            icon: _isBusy
                ? const SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2))
                : const Icon(Icons.cloud_upload_rounded),
            label: Text(_isBuilding ? 'Đang ghép PDF…' : (_isSubmitting ? 'Đang tải lên…' : 'Tải lên')),
            style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
          ),
        ),
      ),
    );
  }

  Widget _buildError(ThemeData theme, String text) => Padding(
        padding: const EdgeInsets.only(top: 8),
        child: Text(text, style: TextStyle(fontSize: 13, color: theme.colorScheme.error)),
      );

  Widget _buildScanStep(ThemeData theme) {
    return _Step(
      number: 1,
      title: 'Bản hợp đồng đã ký',
      children: [
        Text(
          'Quét đủ các trang của bản giấy đã ký và đóng dấu. Các trang được ghép thành một tệp PDF.',
          style: theme.textTheme.bodySmall,
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (_scannerAvailable)
              FilledButton.tonalIcon(
                onPressed: _isBusy ? null : _scanWithCamera,
                icon: const Icon(Icons.document_scanner_rounded),
                label: Text(_pageImages.isEmpty ? 'Quét bằng camera' : 'Quét thêm trang'),
              ),
            OutlinedButton.icon(
              onPressed: _isBusy ? null : _pickFiles,
              icon: const Icon(Icons.upload_file_rounded),
              label: const Text('Chọn PDF / ảnh'),
            ),
          ],
        ),
        if (_fileError != null) _buildError(theme, _fileError!),
        if (_pickedPdf != null) ...[
          const SizedBox(height: 12),
          ListTile(
            contentPadding: EdgeInsets.zero,
            leading: const Icon(Icons.picture_as_pdf_rounded, color: AppColors.statusError),
            title: Text(_pickedPdf!.fileName),
            subtitle: Text(AppFormatUtils.formatFileSize(_pickedPdf!.bytes.length)),
            trailing: IconButton(
              icon: const Icon(Icons.delete_outline_rounded),
              tooltip: 'Bỏ tệp',
              onPressed: _isBusy ? null : _removePickedPdf,
            ),
          ),
        ],
        if (_pageImages.isNotEmpty) ...[
          const SizedBox(height: 12),
          Text('${_pageImages.length} trang', style: theme.textTheme.labelLarge),
          const SizedBox(height: 8),
          SizedBox(
            height: 150,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: _pageImages.length,
              separatorBuilder: (_, _) => const SizedBox(width: 8),
              itemBuilder: (context, index) => _PageThumbnail(
                image: _pageImages[index],
                pageNumber: index + 1,
                onRemove: _isBusy ? null : () => _removePage(index),
              ),
            ),
          ),
        ],
      ],
    );
  }

  Widget _buildSignDateStep(ThemeData theme) {
    return _Step(
      number: 2,
      title: 'Ngày ký trên tài liệu',
      children: [
        OutlinedButton.icon(
          onPressed: _isBusy ? null : _pickSignDate,
          icon: const Icon(Icons.event_rounded),
          label: Text(_signDate == null ? 'Chọn ngày ký' : AppFormatUtils.formatDate(_signDate)),
        ),
        if (_dateError != null)
          _buildError(theme, _dateError!)
        else
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text('Ngày ký ghi trên bản giấy, không phải ngày tải tệp lên.', style: theme.textTheme.bodySmall),
          ),
      ],
    );
  }
}

class _Step extends StatelessWidget {
  final int number;
  final String title;
  final List<Widget> children;

  const _Step({required this.number, required this.title, required this.children});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            CircleAvatar(
              radius: 13,
              backgroundColor: theme.colorScheme.primary,
              child: Text(
                '$number',
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: theme.colorScheme.onPrimary),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(child: Text(title, style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold))),
          ],
        ),
        const SizedBox(height: 10),
        ...children,
      ],
    );
  }
}

class _PageThumbnail extends StatelessWidget {
  final Uint8List image;
  final int pageNumber;
  final VoidCallback? onRemove;

  const _PageThumbnail({required this.image, required this.pageNumber, this.onRemove});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Stack(
      children: [
        ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: Container(
            width: 106,
            height: 150,
            color: theme.colorScheme.outlineVariant.withValues(alpha: 0.3),
            // cacheWidth keeps a 12 MP photo from being decoded at full size for a thumbnail.
            child: Image.memory(image, fit: BoxFit.cover, cacheWidth: 240),
          ),
        ),
        Positioned(
          left: 6,
          bottom: 6,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(
              color: Colors.black.withValues(alpha: 0.6),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Text('$pageNumber', style: const TextStyle(color: Colors.white, fontSize: 12)),
          ),
        ),
        Positioned(
          right: 0,
          top: 0,
          child: IconButton(
            icon: const Icon(Icons.close_rounded, size: 18),
            tooltip: 'Xóa trang $pageNumber',
            onPressed: onRemove,
            style: IconButton.styleFrom(
              backgroundColor: Colors.black.withValues(alpha: 0.55),
              foregroundColor: Colors.white,
              minimumSize: const Size(30, 30),
              padding: EdgeInsets.zero,
            ),
          ),
        ),
      ],
    );
  }
}
