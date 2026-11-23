import 'package:flutter_dotenv/flutter_dotenv.dart';

abstract class AppConfig {
  /// Base API URL duy nhất được định nghĩa tại file .env
  static String get apiBaseUrl {
    final envUrl = dotenv.env['API_BASE_URL'];
    if (envUrl != null && envUrl.isNotEmpty) {
      return envUrl;
    }
    return const String.fromEnvironment('API_BASE_URL');
  }

  /// Máy chủ API nội bộ (không gồm /api); mặc định cùng máy chủ với [apiBaseUrl].
  static String get internalApiBaseUrl {
    final own = dotenv.env['API_INTERNAL_BASE_URL'];
    final url = own != null && own.isNotEmpty ? own : apiBaseUrl;
    return url.replaceFirst(RegExp(r'/+$'), '').replaceFirst(RegExp(r'/api$'), '');
  }

  /// Optional: the host Android uses instead of `localhost` (e.g. 10.0.2.2 on the emulator). Empty: keep localhost,
  /// reached through `adb reverse`.
  static String? get androidHostAlias {
    final alias = dotenv.env['API_ANDROID_HOST']?.trim();
    return alias == null || alias.isEmpty ? null : alias;
  }

  static const String appName = 'Digitizing Contract';

  static const int apiTimeoutSeconds = 30;
}
