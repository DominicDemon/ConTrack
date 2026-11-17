import 'dart:typed_data';

// The internal API sends every long as a string, so ids stay strings here and
// counts are parsed from either form.
int? _toIntOrNull(dynamic value) =>
    value is int ? value : int.tryParse(value?.toString() ?? '');

int _toInt(dynamic value) => _toIntOrNull(value) ?? 0;

double _toDouble(dynamic value) =>
    value is num ? value.toDouble() : double.tryParse(value?.toString() ?? '') ?? 0;

DateTime? _toDate(dynamic value) =>
    value == null ? null : DateTime.tryParse(value.toString());

List<T> _toList<T>(dynamic value, T Function(Map<String, dynamic>) parse) =>
    value is List ? value.whereType<Map<String, dynamic>>().map(parse).toList() : <T>[];

/// GET /api/internal/scan-access: the employee and their permissions.
class ScanAccess {
  static const String viewPermission = 'contract.signed_evidence.view';
  static const String uploadPermission = 'contract.signed_evidence.upload';

  final String employeeId;
  final String username;
  final String displayName;
  final Set<String> permissions;

  ScanAccess({
    required this.employeeId,
    required this.username,
    required this.displayName,
    required this.permissions,
  });

  bool get canView => permissions.contains(viewPermission);
  bool get canUpload => permissions.contains(uploadPermission);

  factory ScanAccess.fromJson(Map<String, dynamic> json) {
    final permissions = json['permissions'];
    return ScanAccess(
      employeeId: json['employeeId']?.toString() ?? '',
      username: json['username']?.toString() ?? '',
      displayName: json['displayName']?.toString() ?? '',
      permissions: permissions is List ? permissions.map((p) => p.toString()).toSet() : <String>{},
    );
  }
}

/// A row of GET /api/internal/contract-documents.
class SignedContractItem {
  final String documentId;
  final String? documentCode;
  final String documentName;
  final String customerName;
  final String contractStatus;
  final double totalAmount;
  final int? versionNo;
  final bool signedOnline;
  final String? paperEvidenceStatus;

  SignedContractItem({
    required this.documentId,
    this.documentCode,
    required this.documentName,
    required this.customerName,
    required this.contractStatus,
    required this.totalAmount,
    this.versionNo,
    required this.signedOnline,
    this.paperEvidenceStatus,
  });

  /// Waiting for a signed copy: not signed online and no copy is being checked.
  bool get needsPaperScan =>
      !signedOnline &&
      contractStatus == 'PendingSignature' &&
      paperEvidenceStatus != 'PendingReview';

  bool matches(String query) {
    final text = query.trim().toLowerCase();
    if (text.isEmpty) return true;
    return [documentCode, documentName, customerName]
        .any((value) => value?.toLowerCase().contains(text) ?? false);
  }

  factory SignedContractItem.fromJson(Map<String, dynamic> json) => SignedContractItem(
        documentId: json['documentId']?.toString() ?? '',
        documentCode: json['documentCode']?.toString(),
        documentName: json['documentName']?.toString() ?? '',
        customerName: json['customerName']?.toString() ?? '',
        contractStatus: json['contractStatus']?.toString() ?? '',
        totalAmount: _toDouble(json['totalAmount']),
        versionNo: _toIntOrNull(json['versionNo']),
        signedOnline: json['signedOnline'] == true,
        paperEvidenceStatus: json['paperEvidenceStatus']?.toString(),
      );
}

class SignedVersion {
  final String versionId;
  final int versionNo;

  SignedVersion({required this.versionId, required this.versionNo});

  factory SignedVersion.fromJson(Map<String, dynamic> json) => SignedVersion(
        versionId: json['versionId']?.toString() ?? '',
        versionNo: _toInt(json['versionNo']),
      );
}

/// What OCR read compared with the contract.
class ScanContentCheck {
  final String field;
  final String? expected;
  final List<String> found;
  final String result;

  ScanContentCheck({
    required this.field,
    this.expected,
    required this.found,
    required this.result,
  });

  factory ScanContentCheck.fromJson(Map<String, dynamic> json) {
    final found = json['found'];
    return ScanContentCheck(
      field: json['field']?.toString() ?? '',
      expected: json['expected']?.toString(),
      found: found is List ? found.map((value) => value.toString()).toList() : <String>[],
      result: json['result']?.toString() ?? '',
    );
  }
}

class EvidenceReview {
  final String result;
  final String reviewerName;
  final DateTime? at;
  final String? note;

  EvidenceReview({required this.result, required this.reviewerName, this.at, this.note});

  factory EvidenceReview.fromJson(Map<String, dynamic> json) => EvidenceReview(
        result: json['result']?.toString() ?? '',
        reviewerName: json['reviewerName']?.toString() ?? '',
        at: _toDate(json['at']),
        note: json['note']?.toString(),
      );
}

class SignedEvidenceItem {
  final String evidenceId;
  final int versionNo;
  final String status;
  final String signDate;
  final String fileName;
  final int fileSizeBytes;
  final int? pageCount;
  final String uploadedByName;
  final DateTime? uploadedAt;
  final EvidenceReview? review;
  final String ocrStatus;
  final String? ocrErrorCode;
  final List<ScanContentCheck> contentChecks;

  SignedEvidenceItem({
    required this.evidenceId,
    required this.versionNo,
    required this.status,
    required this.signDate,
    required this.fileName,
    required this.fileSizeBytes,
    this.pageCount,
    required this.uploadedByName,
    this.uploadedAt,
    this.review,
    required this.ocrStatus,
    this.ocrErrorCode,
    required this.contentChecks,
  });

  bool get isReading => ocrStatus == 'Pending' || ocrStatus == 'Processing';

  factory SignedEvidenceItem.fromJson(Map<String, dynamic> json) {
    final uploadedBy = json['uploadedBy'];
    final review = json['review'];
    final keywords = json['keywords'];
    return SignedEvidenceItem(
      evidenceId: json['evidenceId']?.toString() ?? '',
      versionNo: _toInt(json['versionNo']),
      status: json['status']?.toString() ?? '',
      signDate: json['signDate']?.toString() ?? '',
      fileName: json['fileName']?.toString() ?? '',
      fileSizeBytes: _toInt(json['fileSizeBytes']),
      pageCount: _toIntOrNull(json['pageCount']),
      uploadedByName: uploadedBy is Map ? uploadedBy['displayName']?.toString() ?? '' : '',
      uploadedAt: _toDate(json['uploadedAt']),
      review: review is Map<String, dynamic> ? EvidenceReview.fromJson(review) : null,
      ocrStatus: keywords is Map ? keywords['status']?.toString() ?? '' : '',
      ocrErrorCode: keywords is Map ? keywords['lastErrorCode']?.toString() : null,
      contentChecks: _toList(json['contentChecks'], ScanContentCheck.fromJson),
    );
  }
}

/// GET /api/internal/contract-documents/{id}/signed-evidence.
class SignedContractOverview {
  final String documentId;
  final String? documentCode;
  final String documentName;
  final String contractStatus;
  final String customerName;
  final double totalAmount;
  final SignedVersion? currentVersion;
  final bool canUpload;
  final String? uploadBlockedReason;
  final List<SignedEvidenceItem> evidences;

  SignedContractOverview({
    required this.documentId,
    this.documentCode,
    required this.documentName,
    required this.contractStatus,
    required this.customerName,
    required this.totalAmount,
    this.currentVersion,
    required this.canUpload,
    this.uploadBlockedReason,
    required this.evidences,
  });

  bool get isReading => evidences.any((evidence) => evidence.isReading);

  factory SignedContractOverview.fromJson(Map<String, dynamic> json) {
    final version = json['currentVersion'];
    return SignedContractOverview(
      documentId: json['documentId']?.toString() ?? '',
      documentCode: json['documentCode']?.toString(),
      documentName: json['documentName']?.toString() ?? '',
      contractStatus: json['contractStatus']?.toString() ?? '',
      customerName: json['customerName']?.toString() ?? '',
      totalAmount: _toDouble(json['totalAmount']),
      currentVersion: version is Map<String, dynamic> ? SignedVersion.fromJson(version) : null,
      canUpload: json['canUpload'] == true,
      uploadBlockedReason: json['uploadBlockedReason']?.toString(),
      evidences: _toList(json['evidences'], SignedEvidenceItem.fromJson),
    );
  }
}

/// The one file sent to the server: a PDF as chosen, or the photos joined into a PDF.
class ScanFile {
  final Uint8List bytes;
  final String fileName;
  final String contentType;

  ScanFile({required this.bytes, required this.fileName, required this.contentType});
}

// Same wording as the web app (Frontend/components/signed-scans/format.ts).
abstract class SignedScanLabels {
  static const Map<String, String> _contractStatus = {
    'Draft': 'Nháp',
    'Negotiating': 'Đang đàm phán',
    'PendingApproval': 'Chờ duyệt',
    'PendingSignature': 'Chờ ký',
    'Signed': 'Đã ký',
    'Completed': 'Hoàn thành',
    'Cancelled': 'Đã hủy',
    'Failed': 'Thất bại',
  };

  static const Map<String, String> _evidenceStatus = {
    'PendingReview': 'Chờ kiểm tra',
    'Accepted': 'Hợp lệ',
    'Rejected': 'Không hợp lệ',
    'Superseded': 'Đã bị thay',
  };

  static const Map<String, String> _ocrError = {
    'OcrEngineUnavailable': 'Máy chủ OCR không phản hồi',
    'OcrEngineFailed': 'Máy chủ OCR không đọc được bản quét',
    'OcrTimeout': 'Đọc quá thời gian cho phép',
    'OcrUnsupportedFile': 'Tệp không phải PDF, JPG hoặc PNG',
    'OcrPdfUnreadable': 'Không mở được tệp PDF (tệp hỏng hoặc đặt mật khẩu)',
    'ScanFileMissing': 'Không tìm thấy tệp của bản quét',
    'ScanFileLocked': 'Tệp đã mã hóa, cần key để đọc lại',
  };

  static const Map<String, String> _checkField = {
    'ContractCode': 'Số hợp đồng',
    'TaxCode': 'Mã số thuế',
    'PhoneNumber': 'Số điện thoại',
    'CustomerName': 'Tên khách hàng',
  };

  static const Map<String, String> _checkResult = {
    'Match': 'Khớp',
    'Mismatch': 'Không khớp',
    'NotFound': 'Không thấy',
    'Pending': 'Chờ đọc',
  };

  static String contractStatus(String key) => _contractStatus[key] ?? key;
  static String evidenceStatus(String key) => _evidenceStatus[key] ?? key;
  static String ocrError(String? code) => _ocrError[code] ?? 'Không đọc được từ khóa';
  static String checkField(String key) => _checkField[key] ?? key;
  static String checkResult(String key) => _checkResult[key] ?? key;

  /// yyyy-MM-dd from the API → dd/MM/yyyy, without a time-zone shift.
  static String signDate(String value) {
    final parts = value.split('-');
    return parts.length == 3 ? '${parts[2]}/${parts[1]}/${parts[0]}' : value;
  }
}
