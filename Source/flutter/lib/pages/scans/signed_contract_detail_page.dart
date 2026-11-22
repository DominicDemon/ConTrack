import 'package:flutter/material.dart';
import '../../models/scans/signed_scan_dto.dart';
import 'customer_contract_scan_capture_page.dart';

/// Customer-owned contract scan actions; this flow has no staff review state.
class SignedContractDetailPage extends StatelessWidget {
  final SignedContractItem? contract;
  final String? documentId;
  final ScanAccess? access;
  const SignedContractDetailPage({super.key, this.contract, this.documentId, this.access});

  @override
  Widget build(BuildContext context) {
    final item = contract ?? SignedContractItem(documentId: documentId ?? '', documentName: '', customerName: '', contractStatus: '', totalAmount: 0, signedOnline: false);
    return Scaffold(
    appBar: AppBar(title: const Text('Hợp đồng của bạn')),
    body: ListView(padding: const EdgeInsets.all(20), children: [
      Text(item.documentCode ?? 'Chưa cấp mã', style: Theme.of(context).textTheme.titleMedium),
      const SizedBox(height: 6), Text(item.documentName),
      const SizedBox(height: 6), Text(SignedScanLabels.contractStatus(item.contractStatus)),
      const SizedBox(height: 20),
      const Text('Bạn có thể tự quét bằng camera hoặc tải tệp bản scan lên. Tệp được lưu ngay trong danh sách scan của hợp đồng này.'),
      const SizedBox(height: 16),
      FilledButton.icon(
        onPressed: () async {
          final saved = await Navigator.of(context).push<bool>(MaterialPageRoute(builder: (_) => CustomerContractScanCapturePage(contract: item)));
          if (saved == true && context.mounted) Navigator.pop(context, true);
        },
        icon: const Icon(Icons.document_scanner_outlined), label: const Text('Tạo bản scan'),
      ),
    ]),
  );
  }
}
