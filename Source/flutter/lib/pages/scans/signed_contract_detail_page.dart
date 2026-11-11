import 'dart:async';
import 'package:flutter/material.dart';
import '../../models/scans/signed_scan_dto.dart';
import '../../services/scans/internal_api_client.dart';
import '../../services/scans/signed_scan_api.dart';
import '../../theme/app_colors.dart';
import '../../utils/app_format_utils.dart';
import '../../widgets/contracts/signing_info_box.dart';
import 'signed_contract_list_page.dart';
import 'signed_scan_capture_page.dart';

/// A contract's signed paper copies: upload one, follow OCR and the review.
class SignedContractDetailPage extends StatefulWidget {
  final String documentId;
  final ScanAccess access;

  const SignedContractDetailPage({super.key, required this.documentId, required this.access});

  @override
  State<SignedContractDetailPage> createState() => _SignedContractDetailPageState();
}

class _SignedContractDetailPageState extends State<SignedContractDetailPage> {
  static const Duration _pollEvery = Duration(seconds: 3);

  SignedContractOverview? _overview;
  String? _error;
  bool _isLoading = false;
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  void _show(SignedContractOverview overview) {
    setState(() {
      _overview = overview;
      _error = null;
    });
    if (overview.isReading) {
      _poll ??= Timer.periodic(_pollEvery, (_) => _load(background: true));
    } else {
      _poll?.cancel();
      _poll = null;
    }
  }

  /// [background] is the OCR poll: a failed round is simply tried again.
  Future<void> _load({bool background = false}) async {
    if (_isLoading) return;
    _isLoading = true;
    try {
      final overview = await SignedScanApi.overview(widget.documentId);
      if (mounted) _show(overview);
    } on InternalApiException catch (error) {
      if (!mounted) return;
      if (error.isUnauthorized) {
        Navigator.of(context).pushNamedAndRemoveUntil('/scans/login', (route) => route.isFirst);
      } else if (!background) {
        setState(() => _error = error.message);
      }
    } finally {
      _isLoading = false;
    }
  }

  Future<void> _capture(SignedContractOverview overview) async {
    final updated = await Navigator.of(context).push<SignedContractOverview>(
      MaterialPageRoute(builder: (_) => SignedScanCapturePage(overview: overview)),
    );
    if (updated != null && mounted) _show(updated);
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final overview = _overview;
    final canCapture = overview != null && overview.canUpload && overview.currentVersion != null && widget.access.canUpload;

    return Scaffold(
      backgroundColor: theme.colorScheme.surface,
      appBar: AppBar(title: const Text('Hợp đồng'), scrolledUnderElevation: 0),
      body: overview == null ? _buildPlaceholder() : _buildContent(theme, overview),
      bottomNavigationBar: canCapture
          ? SafeArea(
              minimum: const EdgeInsets.fromLTRB(16, 8, 16, 16),
              child: FilledButton.icon(
                onPressed: () => _capture(overview),
                icon: const Icon(Icons.document_scanner_rounded),
                label: const Text('Tải bản ký lên'),
                style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(52)),
              ),
            )
          : null,
    );
  }

  Widget _buildPlaceholder() {
    if (_error == null) return const Center(child: CircularProgressIndicator());
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(_error!, textAlign: TextAlign.center),
            const SizedBox(height: 12),
            OutlinedButton(onPressed: _load, child: const Text('Thử lại')),
          ],
        ),
      ),
    );
  }

  Widget _buildContent(ThemeData theme, SignedContractOverview overview) {
    final muted = theme.colorScheme.onSurface.withValues(alpha: 0.6);
    final version = overview.currentVersion;

    return RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
        children: [
          Text(
            overview.documentCode ?? 'Chưa cấp mã',
            style: TextStyle(fontWeight: FontWeight.bold, color: theme.colorScheme.primary),
          ),
          const SizedBox(height: 4),
          Text(overview.documentName, style: theme.textTheme.titleMedium),
          const SizedBox(height: 4),
          Text(overview.customerName, style: TextStyle(color: muted)),
          const SizedBox(height: 8),
          Text(
            '${AppFormatUtils.formatMoney(overview.totalAmount, '₫')} · '
            '${SignedScanLabels.contractStatus(overview.contractStatus)}'
            '${version == null ? '' : ' · Phiên bản ${version.versionNo}'}',
          ),
          const SizedBox(height: 16),
          if (overview.uploadBlockedReason != null)
            SigningInfoBox(
              icon: Icons.info_outline_rounded,
              color: AppColors.statusInfo,
              text: overview.uploadBlockedReason!,
            )
          else if (!widget.access.canUpload)
            const SigningInfoBox(
              icon: Icons.lock_outline_rounded,
              color: AppColors.statusWarning,
              text: 'Tài khoản chỉ có quyền xem, chưa được tải bản ký lên.',
            ),
          const SizedBox(height: 20),
          const Text('Bản ký đã tải lên', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          const SizedBox(height: 10),
          if (overview.evidences.isEmpty)
            Text('Chưa có bản ký nào.', style: TextStyle(color: muted))
          else
            ...overview.evidences.map(
              (evidence) => Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: _EvidenceCard(evidence: evidence),
              ),
            ),
          const SizedBox(height: 4),
          Text(
            'Mở tệp bằng key, xác nhận hợp lệ và thay bản ký được thực hiện trên web.',
            style: TextStyle(fontSize: 13, color: muted),
          ),
        ],
      ),
    );
  }
}

class _EvidenceCard extends StatelessWidget {
  final SignedEvidenceItem evidence;

  const _EvidenceCard({required this.evidence});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final muted = theme.colorScheme.onSurface.withValues(alpha: 0.6);
    final review = evidence.review;
    final pages = evidence.pageCount == null ? '' : ' · ${evidence.pageCount} trang';

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.6)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text('Phiên bản ${evidence.versionNo}', style: const TextStyle(fontWeight: FontWeight.w600))),
              EvidenceStatusChip(status: evidence.status),
            ],
          ),
          const SizedBox(height: 8),
          Text(evidence.fileName, maxLines: 2, overflow: TextOverflow.ellipsis),
          const SizedBox(height: 4),
          Text(
            'Ngày ký ${SignedScanLabels.signDate(evidence.signDate)} · '
            '${AppFormatUtils.formatFileSize(evidence.fileSizeBytes)}$pages',
            style: TextStyle(fontSize: 13, color: muted),
          ),
          Text(
            '${evidence.uploadedByName} · ${AppFormatUtils.formatDateTime(evidence.uploadedAt)}',
            style: TextStyle(fontSize: 13, color: muted),
          ),
          const SizedBox(height: 10),
          _buildOcr(muted),
          if (review != null) ...[
            const Divider(height: 20),
            Text(
              'Người kiểm tra: ${review.reviewerName}',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            if (review.note != null && review.note!.isNotEmpty) ...[
              const SizedBox(height: 2),
              Text(review.note!),
            ],
          ],
        ],
      ),
    );
  }

  Widget _buildOcr(Color muted) {
    if (evidence.isReading) {
      return Row(
        children: [
          const SizedBox.square(dimension: 16, child: CircularProgressIndicator(strokeWidth: 2)),
          const SizedBox(width: 8),
          Expanded(child: Text(evidence.ocrStatus == 'Pending' ? 'Đang chờ đọc bản scan…' : 'Đang đọc bản scan…')),
        ],
      );
    }
    if (evidence.ocrStatus == 'Failed') {
      return Text(
        'OCR lỗi: ${SignedScanLabels.ocrError(evidence.ocrErrorCode)}',
        style: const TextStyle(color: AppColors.statusError),
      );
    }
    return Column(
      children: evidence.contentChecks.map((check) {
        final color = switch (check.result) {
          'Match' => AppColors.statusSuccess,
          'Mismatch' => AppColors.statusError,
          _ => muted,
        };
        return Padding(
          padding: const EdgeInsets.symmetric(vertical: 2),
          child: Row(
            children: [
              Expanded(child: Text(SignedScanLabels.checkField(check.field))),
              Text(
                SignedScanLabels.checkResult(check.result),
                style: TextStyle(fontWeight: FontWeight.w600, color: color),
              ),
            ],
          ),
        );
      }).toList(),
    );
  }
}
