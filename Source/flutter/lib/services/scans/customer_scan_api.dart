import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart' as http_parser;
import 'package:shared_preferences/shared_preferences.dart';
import '../../config/app_config.dart';
import '../../models/scans/signed_scan_dto.dart';

class CustomerScanException implements Exception {
  final int? statusCode;
  final String message;
  const CustomerScanException(this.message, [this.statusCode]);
  bool get isUnauthorized => statusCode == 401;
  @override
  String toString() => message;
}

class CustomerScan {
  final String fileId, kind, fileName, uploadedAt, matchedBy, status;
  final String? contractCode, contractName;
  final bool ready;
  const CustomerScan({required this.fileId, required this.kind, required this.fileName, required this.uploadedAt, required this.matchedBy, required this.ready, required this.status, this.contractCode, this.contractName});
  factory CustomerScan.fromJson(Map<String, dynamic> json) => CustomerScan(
    fileId: '${json['fileId'] ?? ''}', kind: '${json['kind'] ?? ''}', fileName: '${json['fileName'] ?? ''}',
    uploadedAt: '${json['uploadedAt'] ?? ''}', matchedBy: '${json['matchedBy'] ?? ''}', ready: json['ready'] == true, status: '${json['status'] ?? 'Ready'}',
    contractCode: json['contractCode']?.toString(), contractName: json['contractName']?.toString());
}

class VaultKeyword {
  final String id, type, value;
  final bool manual;
  const VaultKeyword({required this.id, required this.type, required this.value, required this.manual});
  factory VaultKeyword.fromJson(Map<String, dynamic> j) => VaultKeyword(id: '${j['id']}', type: '${j['type']}', value: '${j['value']}', manual: j['manual'] == true);
}

/// A document of the customer's own vault (GET /api/customer/vault).
class VaultItem {
  final String id, title, fileName, contentType, uploadedAt, ocrStatus;
  final String? note, category, expiresOn, trashedAt;
  final int sizeBytes;
  final int? pageCount, remindDays, daysLeft;
  final List<VaultKeyword> keywords;
  final int sameContractCode;
  const VaultItem({required this.id, required this.title, required this.fileName, required this.contentType, required this.uploadedAt, required this.ocrStatus,
    this.note, this.category, this.expiresOn, this.trashedAt, required this.sizeBytes, this.pageCount, this.remindDays, this.daysLeft, required this.keywords, required this.sameContractCode});
  static int? _int(Object? v) => v == null ? null : int.tryParse('$v');
  factory VaultItem.fromJson(Map<String, dynamic> j) => VaultItem(
    id: '${j['id']}', title: '${j['title'] ?? ''}', fileName: '${j['fileName'] ?? ''}', contentType: '${j['contentType'] ?? ''}', uploadedAt: '${j['uploadedAt'] ?? ''}',
    ocrStatus: '${j['ocrStatus'] ?? ''}', note: j['note']?.toString(), category: j['category']?.toString(), expiresOn: j['expiresOn']?.toString(), trashedAt: j['trashedAt']?.toString(),
    sizeBytes: _int(j['sizeBytes']) ?? 0, pageCount: _int(j['pageCount']), remindDays: _int(j['remindDays']), daysLeft: _int(j['daysLeft']),
    keywords: ((j['keywords'] as List?) ?? []).whereType<Map<String, dynamic>>().map(VaultKeyword.fromJson).toList(),
    sameContractCode: ((j['sameContractCode'] as List?) ?? []).length);
}

class VaultUsage {
  final int usedBytes, limitBytes, files, inTrash;
  const VaultUsage(this.usedBytes, this.limitBytes, this.files, this.inTrash);
  factory VaultUsage.fromJson(Map<String, dynamic> j) =>
      VaultUsage(int.tryParse('${j['usedBytes']}') ?? 0, int.tryParse('${j['limitBytes']}') ?? 1, int.tryParse('${j['files']}') ?? 0, int.tryParse('${j['inTrash']}') ?? 0);
}

/// The file key, kept in memory only for 15 minutes after its last use, shared by the scan and vault screens.
class RememberedFileKey {
  RememberedFileKey._();
  static const minutes = 15;
  static String? _key;
  static DateTime? _until;
  static String get value {
    if (_until == null || DateTime.now().isAfter(_until!)) { _key = null; _until = null; }
    return _key ?? '';
  }
  static void touch(String key) {
    if (key.trim().isEmpty) { forget(); return; }
    _key = key.trim(); _until = DateTime.now().add(const Duration(minutes: minutes));
  }
  static void forget() { _key = null; _until = null; }
}

class CustomerScanApi {
  CustomerScanApi._();
  static final CustomerScanApi instance = CustomerScanApi._();
  static const _cookieKey = 'customer_portal_scan_cookies';
  static final _separator = RegExp(r',(?=\s*[^;,=\s]+=)');
  final _http = http.Client();
  final _cookies = <String, String>{};
  Future<void>? _loaded;
  String? _csrf;

  Uri _url(String path) => Uri.parse('${AppConfig.internalApiBaseUrl}/api/customer$path');
  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_cookieKey);
    if (raw != null) (jsonDecode(raw) as Map<String, dynamic>).forEach((k, v) => _cookies[k] = '$v');
  }
  Future<void> _ready() async => await (_loaded ??= _load());
  void _readCookies(http.Response response) {
    final raw = response.headers['set-cookie'];
    if (raw == null) return;
    for (final value in raw.split(_separator)) {
      final pair = value.split(';').first;
      final i = pair.indexOf('=');
      if (i <= 0) continue;
      final name = pair.substring(0, i).trim(), content = pair.substring(i + 1).trim();
      if (content.isEmpty) { _cookies.remove(name); } else { _cookies[name] = content; }
    }
    SharedPreferences.getInstance().then((p) => p.setString(_cookieKey, jsonEncode(_cookies)));
  }
  Map<String, String> get _headers => {'Accept': 'application/json', if (_cookies.isNotEmpty) 'Cookie': _cookies.entries.map((e) => '${e.key}=${e.value}').join('; ')};

  Future<dynamic> _request(String path, {String method = 'GET', Object? body}) async {
    await _ready();
    if (method != 'GET' && _csrf == null) _csrf = '${(await _request('/auth/csrf'))['token']}';
    final headers = {..._headers, if (body != null) 'Content-Type': 'application/json; charset=utf-8', if (_csrf != null) 'X-CSRF-TOKEN': _csrf!};
    final request = http.Request(method, _url(path))..headers.addAll(headers);
    if (body != null) request.body = jsonEncode(body);
    final response = await _http.send(request).then(http.Response.fromStream);
    _readCookies(response);
    dynamic data;
    try { data = response.body.isEmpty ? null : jsonDecode(response.body); } catch (_) {}
    if (response.statusCode < 200 || response.statusCode >= 300) {
      if (response.statusCode == 400) _csrf = null;
      if (response.statusCode == 401) {
        _cookies.clear(); _csrf = null;
        SharedPreferences.getInstance().then((p) => p.remove(_cookieKey));
      }
      final message = data is Map ? data['message']?.toString() : null;
      throw CustomerScanException(message?.isNotEmpty == true ? message! : 'Yêu cầu thất bại (${response.statusCode}).', response.statusCode);
    }
    return data;
  }

  Future<Map<String, dynamic>> login(String phone, String password) async => (await _request('/auth/login', method: 'POST', body: {'phone': phone, 'password': password})) as Map<String, dynamic>;
  Future<Map<String, dynamic>> me() async => (await _request('/auth/me')) as Map<String, dynamic>;
  Future<List<CustomerScan>> list() async => ((await _request('/scans')) as List).whereType<Map<String, dynamic>>().map(CustomerScan.fromJson).toList();
  Future<List<SignedContractItem>> contracts() async => ((await _request('/scans/contracts')) as List).whereType<Map<String, dynamic>>().map(SignedContractItem.fromJson).toList();
  Future<bool> hasKey() async => (await _request('/file-key') as Map)['hasKey'] == true;
  Future<String> sendKeyCode(String password) async => '${(await _request('/file-key/code', method: 'POST', body: {'password': password}) as Map)['sentTo']}';
  Future<void> logout() async {
    try { await _request('/auth/logout', method: 'POST', body: {}); } catch (_) {}
    _cookies.clear(); _csrf = null;
    final prefs = await SharedPreferences.getInstance(); await prefs.remove(_cookieKey);
  }
  Future<void> setKey(String password, String code, String newKey) async => _request('/file-key', method: 'POST', body: {'password': password, 'code': code, 'newKey': newKey});
  Future<void> changeKey(String currentKey, String newKey) async => _request('/file-key/change', method: 'POST', body: {'currentKey': currentKey, 'newKey': newKey});
  Future<void> upload(String documentId, {required ScanFile file}) async =>
      _multipart('/scans/contracts/$documentId/scans', file, const {}, 'Không tải được bản scan.');

  Future<dynamic> _multipart(String path, ScanFile file, Map<String, String> fields, String failure) async {
    await _ready();
    if (_csrf == null) _csrf = '${(await _request('/auth/csrf'))['token']}';
    final request = http.MultipartRequest('POST', _url(path))
      ..headers.addAll({..._headers, 'X-CSRF-TOKEN': _csrf!})
      ..fields.addAll(fields)
      ..files.add(http.MultipartFile.fromBytes('file', file.bytes, filename: file.fileName, contentType: http_parser.MediaType.parse(file.contentType)));
    final response = await _http.send(request).then(http.Response.fromStream);
    _readCookies(response);
    dynamic data;
    try { data = response.body.isEmpty ? null : jsonDecode(response.body); } catch (_) {}
    if (response.statusCode < 200 || response.statusCode >= 300) {
      if (response.statusCode == 400) _csrf = null;
      throw CustomerScanException(data is Map ? '${data['message'] ?? failure}' : failure, response.statusCode);
    }
    return data;
  }

  /// A call that opens a file with the customer's key (X-File-Key); the key is remembered on success.
  Future<http.Response> _withKey(String path, String key, {String method = 'GET', Object? body, String failure = 'Không mở được tệp.'}) async {
    await _ready();
    if (method != 'GET' && _csrf == null) _csrf = '${(await _request('/auth/csrf'))['token']}';
    final request = http.Request(method, _url(path))
      ..headers.addAll({..._headers, 'X-File-Key': key.trim(),
        if (method != 'GET') 'X-CSRF-TOKEN': _csrf!, if (body != null) 'Content-Type': 'application/json; charset=utf-8'});
    if (body != null) request.body = jsonEncode(body);
    final response = await _http.send(request).then(http.Response.fromStream);
    _readCookies(response);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      if (response.statusCode == 400 && method != 'GET') _csrf = null;
      dynamic data; try { data = jsonDecode(response.body); } catch (_) {}
      throw CustomerScanException(data is Map ? '${data['message'] ?? failure}' : failure, response.statusCode);
    }
    RememberedFileKey.touch(key);
    return response;
  }

  Future<http.Response> open(String fileId, String key) => _withKey('/scans/$fileId/file', key, failure: 'Không mở được bản scan.');

  // The customer's own vault: documents only they can open.
  Future<List<VaultItem>> vault({String? query, String? category, bool trash = false}) async {
    final params = {if (query?.trim().isNotEmpty == true) 'q': query!.trim(), if (category?.isNotEmpty == true) 'category': category!, if (trash) 'trash': 'true'};
    final path = params.isEmpty ? '/vault' : '/vault?${Uri(queryParameters: params).query}';
    return ((await _request(path)) as List).whereType<Map<String, dynamic>>().map(VaultItem.fromJson).toList();
  }
  Future<VaultUsage> vaultUsage() async => VaultUsage.fromJson((await _request('/vault/usage')) as Map<String, dynamic>);
  Future<List<String>> vaultCategories() async => ((await _request('/vault/categories')) as List).map((e) => '$e').toList();
  Future<void> vaultUpload(ScanFile file, {String? title, String? note, String? category, String? expiresOn}) => _multipart('/vault', file,
      {'title': title ?? '', 'note': note ?? '', 'category': category ?? '', 'expiresOn': expiresOn ?? ''}, 'Không lưu được tài liệu.');
  Future<void> vaultEdit(String id, {required String title, String? note, String? category, String? expiresOn, int? remindDays}) =>
      _request('/vault/$id', method: 'PUT', body: {'title': title, 'note': note, 'category': category, 'expiresOn': expiresOn, 'remindDays': remindDays});
  Future<void> vaultTrash(String id) => _request('/vault/$id', method: 'DELETE');
  Future<void> vaultRestore(String id) => _request('/vault/$id/restore', method: 'POST', body: {});
  Future<void> vaultPurge(String id) => _request('/vault/trash/$id', method: 'DELETE');
  Future<void> vaultAddKeyword(String id, String type, String value) => _request('/vault/$id/keywords', method: 'POST', body: {'type': type, 'value': value});
  Future<void> vaultRemoveKeyword(String id, String keywordId) => _request('/vault/$id/keywords/$keywordId', method: 'DELETE');
  Future<http.Response> vaultOpen(String id, String key) => _withKey('/vault/$id/file', key);
  Future<void> vaultReread(String id, String key) => _withKey('/vault/$id/reread', key, method: 'POST', failure: 'Không đọc lại được.');
  Future<List<String>> vaultEmail(String id, String key, List<String> recipients, String? note) async {
    final response = await _withKey('/vault/$id/email', key, method: 'POST', body: {'recipients': recipients, 'note': note}, failure: 'Không gửi được email.');
    return ((jsonDecode(response.body) as Map)['sent'] as List).map((e) => '$e').toList();
  }
}
