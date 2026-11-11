import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';
import 'package:shared_preferences/shared_preferences.dart';
import '../../config/app_config.dart';
import '../../models/scans/signed_scan_dto.dart';

class InternalApiException implements Exception {
  /// Null when the request never got an answer from the server.
  final int? statusCode;
  final String message;
  final String? code;

  InternalApiException({this.statusCode, required this.message, this.code});

  bool get isUnauthorized => statusCode == 401;

  @override
  String toString() => message;
}

/// The internal API under /api/internal: session cookie plus CSRF token.
///
/// Kept apart from [ApiClient], which talks to the multi-tenant API and signs the
/// user out when the network drops. Here a dropped connection must not end the
/// session, or an upload failing on a weak signal would lose the scanned pages.
class InternalApiClient {
  InternalApiClient(this._http);

  static InternalApiClient instance = InternalApiClient(http.Client());

  static const String _cookiesKey = 'internal_session_cookies';
  static const Duration _requestTimeout = Duration(seconds: 30);
  static const Duration _uploadTimeout = Duration(minutes: 3);

  /// Commas also occur inside `expires=`; only one followed by `name=` starts a cookie.
  static final RegExp _cookieSeparator = RegExp(r',(?=\s*[^;,=\s]+=)');

  final http.Client _http;
  final Map<String, String> _cookies = {};
  Future<void>? _loaded;
  String? _csrfToken;

  Future<void> _loadCookies() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final saved = prefs.getString(_cookiesKey);
      if (saved != null) {
        (jsonDecode(saved) as Map<String, dynamic>).forEach((name, value) => _cookies[name] = value.toString());
      }
    } catch (_) {
      // Unreadable storage only means signing in again.
    }
  }

  Future<void> _saveCookies() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_cookiesKey, jsonEncode(_cookies));
    } catch (_) {}
  }

  Future<void> clearSession() async {
    await (_loaded ??= _loadCookies());
    _cookies.clear();
    _csrfToken = null;
    await _saveCookies();
  }

  void _readCookies(http.BaseResponse response) {
    final header = response.headers['set-cookie'];
    if (header == null) return;
    for (final cookie in header.split(_cookieSeparator)) {
      final pair = cookie.split(';').first;
      final separator = pair.indexOf('=');
      if (separator <= 0) continue;
      final name = pair.substring(0, separator).trim();
      final value = pair.substring(separator + 1).trim();
      if (value.isEmpty) {
        _cookies.remove(name);
      } else {
        _cookies[name] = value;
      }
    }
    unawaited(_saveCookies());
  }

  Uri _url(String path) => Uri.parse('${AppConfig.internalApiBaseUrl}/api/internal$path');

  Future<dynamic> _send(http.BaseRequest request, Duration timeout) async {
    await (_loaded ??= _loadCookies());
    request.headers['Accept'] = 'application/json';
    if (_cookies.isNotEmpty) {
      request.headers['Cookie'] = _cookies.entries.map((c) => '${c.key}=${c.value}').join('; ');
    }

    final http.Response response;
    try {
      response = await http.Response.fromStream(await _http.send(request).timeout(timeout)).timeout(timeout);
    } on TimeoutException {
      throw InternalApiException(message: 'Máy chủ phản hồi quá lâu. Vui lòng thử lại.');
    } on http.ClientException {
      throw InternalApiException(message: 'Không kết nối được máy chủ. Vui lòng thử lại.');
    }

    _readCookies(response);
    dynamic body;
    if (response.bodyBytes.isNotEmpty) {
      try {
        body = jsonDecode(utf8.decode(response.bodyBytes));
      } catch (_) {
        body = null;
      }
    }
    if (response.statusCode >= 200 && response.statusCode < 300) return body;

    if (response.statusCode == 401) {
      await clearSession();
    } else if (response.statusCode == 400) {
      // A 400 may mean the token expired with the session: the next call asks for a new one.
      _csrfToken = null;
    }
    final message = body is Map ? body['message']?.toString() : null;
    throw InternalApiException(
      statusCode: response.statusCode,
      message: message == null || message.trim().isEmpty ? 'Yêu cầu thất bại (${response.statusCode}).' : message,
      code: body is Map ? body['code']?.toString() : null,
    );
  }

  Future<String> _csrf() async {
    final token = _csrfToken;
    if (token != null) return token;
    final body = await get('/auth/csrf');
    return _csrfToken = (body as Map)['token'].toString();
  }

  Future<dynamic> get(String path) => _send(http.Request('GET', _url(path)), _requestTimeout);

  Future<dynamic> postJson(String path, Object? body) async {
    final token = await _csrf();
    final request = http.Request('POST', _url(path))
      ..headers['X-CSRF-TOKEN'] = token
      ..headers['Content-Type'] = 'application/json; charset=utf-8'
      ..body = jsonEncode(body);
    return _send(request, _requestTimeout);
  }

  Future<dynamic> postFile(String path, {required Map<String, String> fields, required ScanFile file}) async {
    final token = await _csrf();
    final request = http.MultipartRequest('POST', _url(path))
      ..headers['X-CSRF-TOKEN'] = token
      ..fields.addAll(fields)
      ..files.add(
        http.MultipartFile.fromBytes(
          'file',
          file.bytes,
          filename: file.fileName,
          contentType: MediaType.parse(file.contentType),
        ),
      );
    if (kDebugMode) debugPrint('[InternalApi] upload ${file.fileName} (${file.bytes.length} bytes)');
    return _send(request, _uploadTimeout);
  }
}
