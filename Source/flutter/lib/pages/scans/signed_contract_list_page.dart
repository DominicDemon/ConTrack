import 'package:flutter/material.dart';
import '../../models/scans/signed_scan_dto.dart';
import '../../services/scans/internal_api_client.dart';
import '../../services/scans/signed_scan_api.dart';
import '../../theme/app_colors.dart';
import '../../utils/app_format_utils.dart';
import '../../widgets/app_skeleton.dart';
import 'signed_contract_detail_page.dart';

/// Contracts waiting for their signed paper copy, and every contract the employee can see.
class SignedContractListPage extends StatefulWidget {
  const SignedContractListPage({super.key});

  @override
  State<SignedContractListPage> createState() => _SignedContractListPageState();
}

class _SignedContractListPageState extends State<SignedContractListPage> {
  final TextEditingController _searchController = TextEditingController();

  ScanAccess? _access;
  List<SignedContractItem>? _items;
  bool _onlyNeedingScan = true;
  bool _isLoading = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  void _toLogin() {
    Navigator.of(context).pushNamedAndRemoveUntil('/scans/login', (route) => route.isFirst);
  }

  /// Keeps the current rows on screen while refreshing.
  Future<void> _load() async {
    if (_isLoading) return;
    setState(() {
      _isLoading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([SignedScanApi.access(), SignedScanApi.contracts()]);
      if (!mounted) return;
      setState(() {
        _access = results[0] as ScanAccess;
        _items = results[1] as List<SignedContractItem>;
      });
    } on InternalApiException catch (error) {
      if (!mounted) return;
      if (error.isUnauthorized) {
        _toLogin();
      } else {
        setState(() => _error = error.message);
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _logout() async {
    await SignedScanApi.logout();
    if (mounted) _toLogin();
  }

  Future<void> _open(SignedContractItem item) async {
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (_) => SignedContractDetailPage(documentId: item.documentId, access: _access!),
      ),
    );
    if (mounted) _load();
  }

  List<SignedContractItem> get _visible => (_items ?? const <SignedContractItem>[])
      .where((item) => (!_onlyNeedingScan || item.needsPaperScan) && item.matches(_searchController.text))
      .toList();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      backgroundColor: theme.colorScheme.surface,
      appBar: AppBar(
        title: const Text('Bản ký giấy'),
        scrolledUnderElevation: 0,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh_rounded),
            tooltip: 'Tải lại',
            onPressed: _isLoading ? null : _load,
          ),
          IconButton(
            icon: const Icon(Icons.logout_rounded),
            tooltip: 'Đăng xuất',
            onPressed: _logout,
          ),
        ],
      ),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 4, 16, 8),
            child: SizedBox(
              width: double.infinity,
              child: SegmentedButton<bool>(
                showSelectedIcon: false,
                segments: const [
                  ButtonSegment(value: true, label: Text('Cần tải bản ký')),
                  ButtonSegment(value: false, label: Text('Tất cả')),
                ],
                selected: {_onlyNeedingScan},
                onSelectionChanged: (selected) => setState(() => _onlyNeedingScan = selected.first),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: TextField(
              controller: _searchController,
              textInputAction: TextInputAction.search,
              decoration: InputDecoration(
                hintText: 'Tìm theo mã, tên hợp đồng, khách hàng',
                prefixIcon: const Icon(Icons.search_rounded),
                suffixIcon: _searchController.text.isEmpty
                    ? null
                    : IconButton(
                        icon: const Icon(Icons.close_rounded),
                        tooltip: 'Xóa tìm kiếm',
                        onPressed: () => setState(_searchController.clear),
                      ),
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
                isDense: true,
              ),
              onChanged: (_) => setState(() {}),
            ),
          ),
          Expanded(child: _buildBody(theme)),
        ],
      ),
    );
  }

  Widget _buildBody(ThemeData theme) {
    if (_items == null && _error == null) {
      return const AppSkeletonList(itemCount: 5, padding: EdgeInsets.symmetric(horizontal: 16));
    }
    if (_items == null) return _buildMessage(theme, Icons.cloud_off_rounded, _error!);

    final items = _visible;
    return RefreshIndicator(
      onRefresh: _load,
      child: items.isEmpty
          ? _buildMessage(
              theme,
              Icons.inventory_2_outlined,
              _onlyNeedingScan ? 'Không có hợp đồng nào đang chờ tải bản ký.' : 'Không có hợp đồng phù hợp.',
            )
          : ListView.separated(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.fromLTRB(16, 4, 16, 24),
              itemCount: items.length,
              separatorBuilder: (_, _) => const SizedBox(height: 12),
              itemBuilder: (context, index) => _ContractCard(item: items[index], onTap: () => _open(items[index])),
            ),
    );
  }

  Widget _buildMessage(ThemeData theme, IconData icon, String text) {
    final muted = theme.colorScheme.onSurface.withValues(alpha: 0.6);
    return ListView(
      physics: const AlwaysScrollableScrollPhysics(),
      padding: const EdgeInsets.all(32),
      children: [
        const SizedBox(height: 48),
        Icon(icon, size: 56, color: muted),
        const SizedBox(height: 12),
        Text(text, textAlign: TextAlign.center, style: TextStyle(color: muted)),
      ],
    );
  }
}

class _ContractCard extends StatelessWidget {
  final SignedContractItem item;
  final VoidCallback onTap;

  const _ContractCard({required this.item, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final muted = theme.colorScheme.onSurface.withValues(alpha: 0.6);
    final paperStatus = item.paperEvidenceStatus;

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: theme.colorScheme.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.6)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    item.documentCode ?? 'Chưa cấp mã',
                    style: TextStyle(fontWeight: FontWeight.bold, color: theme.colorScheme.primary),
                  ),
                ),
                Text(SignedScanLabels.contractStatus(item.contractStatus), style: TextStyle(fontSize: 12, color: muted)),
              ],
            ),
            const SizedBox(height: 6),
            Text(item.documentName, style: theme.textTheme.titleSmall, maxLines: 2, overflow: TextOverflow.ellipsis),
            const SizedBox(height: 6),
            Row(
              children: [
                Icon(Icons.business_outlined, size: 16, color: muted),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    item.customerName,
                    style: TextStyle(fontSize: 13, color: muted),
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: Text(
                    AppFormatUtils.formatMoney(item.totalAmount, '₫'),
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                ),
                if (item.signedOnline)
                  Text('Ký online', style: TextStyle(fontSize: 12, color: muted))
                else if (paperStatus != null)
                  EvidenceStatusChip(status: paperStatus),
                const SizedBox(width: 4),
                Icon(Icons.chevron_right_rounded, color: muted),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// A status always reads as words; the color only backs them up.
class EvidenceStatusChip extends StatelessWidget {
  final String status;

  const EvidenceStatusChip({super.key, required this.status});

  @override
  Widget build(BuildContext context) {
    final color = switch (status) {
      'Accepted' => AppColors.statusSuccess,
      'Rejected' => AppColors.statusError,
      'PendingReview' => AppColors.statusWarning,
      _ => Theme.of(context).colorScheme.onSurface.withValues(alpha: 0.6),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Text(
        SignedScanLabels.evidenceStatus(status),
        style: TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: color),
      ),
    );
  }
}
