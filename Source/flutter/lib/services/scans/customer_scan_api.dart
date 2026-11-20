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
  Future<void> upload(String documentId, {required ScanFile file}) async {
    await _ready();
    if (_csrf == null) _csrf = '${(await _request('/auth/csrf'))['token']}';
    final request = http.MultipartRequest('POST', _url('/scans/contracts/$documentId/scans'))
      ..headers.addAll({..._headers, 'X-CSRF-TOKEN': _csrf!})
      ..files.add(http.MultipartFile.fromBytes('file', file.bytes, filename: file.fileName, contentType: http_parser.MediaType.parse(file.contentType)));
    final response = await _http.send(request).then(http.Response.fromStream);
    _readCookies(response);
    dynamic data;
    try { data = response.body.isEmpty ? null : jsonDecode(response.body); } catch (_) {}
    if (response.statusCode < 200 || response.statusCode >= 300) {
      if (response.statusCode == 400) _csrf = null;
      throw CustomerScanException(data is Map ? '${data['message'] ?? 'Không tải được bản scan.'}' : 'Không tải được bản scan.', response.statusCode);
    }
  }
  Future<http.Response> open(String fileId, String key) async {
    await _ready();
    final response = await _http.get(_url('/scans/$fileId/file'), headers: {..._headers, 'X-File-Key': key});
    _readCookies(response);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      dynamic data; try { data = jsonDecode(response.body); } catch (_) {}
      throw CustomerScanException(data is Map ? '${data['message'] ?? 'Không mở được bản scan.'}' : 'Không mở được bản scan.', response.statusCode);
    }
    return response;
  }
}
