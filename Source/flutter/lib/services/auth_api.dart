import '../models/auth_dto.dart';
import 'api_client.dart';

/// Employee sign in against the internal API (/api/internal/auth/*): a session cookie, and a CSRF token on every
/// POST. The internal user is mapped onto the app's existing DTOs, so the other screens keep working unchanged.
class AuthApi {
  static final ApiClient _apiClient = ApiClient();

  static Future<Map<String, String>> _csrf() async {
    final response = await _apiClient.get('/internal/auth/csrf');
    return {'X-CSRF-TOKEN': '${(response as Map<String, dynamic>)['token']}'};
  }

  /// Đăng nhập bằng tên đăng nhập và mật khẩu nội bộ.
  static Future<LoginResponseDto> login({
    required String accountName,
    required String password,
  }) async {
    final response = await _apiClient.post(
      '/internal/auth/login',
      body: {'username': accountName, 'password': password},
      headers: await _csrf(),
    );
    final user = response as Map<String, dynamic>;
    return LoginResponseDto(
      message: 'Đăng nhập thành công!',
      employeeId: int.tryParse('${user['id']}') ?? 0,
      employeeName: user['name']?.toString(),
    );
  }

  /// Tài khoản đang đăng nhập (401 khi chưa đăng nhập hoặc phiên đã hết).
  static Future<UserProfileDto> getMe() async {
    final user = await _apiClient.get('/internal/auth/me') as Map<String, dynamic>;
    final department = user['department']?.toString();
    final manager = user['isManager'] == true;
    return UserProfileDto.fromJson({
      'employeeId': user['id'],
      'employeeAccount': user['username'] ?? '',
      'employeeFullName': user['name'],
      // Shown as the role badge in the profile menu: the department, not the raw permission list.
      'userRoles': department == null || department.isEmpty ? (manager ? 'Quản lý' : 'Nhân viên') : (manager ? 'Trưởng phòng · $department' : department),
      'status': 1,
    });
  }

  /// Đăng xuất khỏi hệ thống
  static Future<String> logout() async {
    try {
      await _apiClient.post('/internal/auth/logout', headers: await _csrf());
    } catch (_) {
      // The session is dropped locally either way.
    } finally {
      await _apiClient.clearCookies();
    }
    return 'Đăng xuất thành công!';
  }
}
