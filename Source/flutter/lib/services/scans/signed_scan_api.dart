import '../../models/scans/signed_scan_dto.dart';
import '../../utils/app_date_utils.dart';
import 'internal_api_client.dart';

/// The part of the paper-signing flow done on a phone (Master_Database_HuongDan F18,
/// input step): list, upload, follow the check. Opening files and reviewing stay on the web.
class SignedScanApi {
  static InternalApiClient get _client => InternalApiClient.instance;

  static Future<void> login({required String username, required String password}) async {
    await _client.postJson('/auth/login', {'username': username, 'password': password});
  }

  static Future<void> logout() async {
    try {
      await _client.postJson('/auth/logout', <String, Object?>{});
    } catch (_) {
      // The session on this phone ends either way.
    } finally {
      await _client.clearSession();
    }
  }

  static Future<ScanAccess> access() async =>
      ScanAccess.fromJson(await _client.get('/scan-access') as Map<String, dynamic>);

  static Future<List<SignedContractItem>> contracts() async {
    final rows = await _client.get('/contract-documents') as List;
    return rows.whereType<Map<String, dynamic>>().map(SignedContractItem.fromJson).toList();
  }

  static Future<SignedContractOverview> overview(String documentId) async => SignedContractOverview.fromJson(
        await _client.get('/contract-documents/$documentId/signed-evidence') as Map<String, dynamic>,
      );

  /// Saves the signed copy; the server reads it with OCR on its own afterwards.
  static Future<SignedContractOverview> upload(
    String documentId, {
    required String versionId,
    required DateTime signDate,
    required ScanFile file,
  }) async {
    final response = await _client.postFile(
      '/contract-documents/$documentId/signed-evidence',
      fields: {'versionId': versionId, 'signDate': AppDateUtils.formatToYMD(signDate)!},
      file: file,
    );
    return SignedContractOverview.fromJson(response as Map<String, dynamic>);
  }
}
