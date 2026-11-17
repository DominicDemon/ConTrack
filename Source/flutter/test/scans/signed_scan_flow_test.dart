import 'dart:convert';
import 'dart:typed_data';
import 'package:cross_file/cross_file.dart';
import 'package:dtc_digitizingcontract/models/scans/signed_scan_dto.dart';
import 'package:dtc_digitizingcontract/pages/scans/scan_login_page.dart';
import 'package:dtc_digitizingcontract/pages/scans/signed_contract_detail_page.dart';
import 'package:dtc_digitizingcontract/pages/scans/signed_contract_list_page.dart';
import 'package:dtc_digitizingcontract/services/scans/internal_api_client.dart';
import 'package:dtc_digitizingcontract/utils/scan_pdf_builder.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:image/image.dart' as img;
import 'package:shared_preferences/shared_preferences.dart';

final class _PickedFile extends PlatformFile {
  @override
  final String name;
  final Uint8List content;

  _PickedFile(this.name, this.content);

  @override
  Future<Uint8List> readAsBytes() async => content;

  @override
  Stream<Uint8List> readAsByteStream() => Stream.value(content);

  @override
  Future<int?> length() async => content.length;

  @override
  int? lengthSync() => content.length;

  @override
  Uri get uri => Uri.file(name);

  @override
  XFile get xFile => XFile.fromData(content, name: name);
}

class _FakePicker extends FilePickerPlatform {
  List<PlatformFile> next = [];

  @override
  Future<List<PlatformFile>> pickFiles({
    String? dialogTitle,
    String? initialDirectory,
    FileType type = FileType.any,
    List<String>? allowedExtensions,
    Function(FilePickerStatus)? onFileLoading,
    int compressionQuality = 0,
    AndroidOptions androidOptions = const AndroidOptions(),
    DarwinOptions darwinOptions = const DarwinOptions(),
    WindowsOptions windowsOptions = const WindowsOptions(),
    LinuxOptions linuxOptions = const LinuxOptions(),
    WebOptions webOptions = const WebOptions(),
  }) async => next;
}

Map<String, Object?> item(String id, String code, {String status = 'PendingSignature', String? paper, bool online = false}) => {
      'documentId': id,
      'documentCode': code,
      'documentName': 'Hợp đồng $code',
      'customerName': 'Công ty $code',
      'contractStatus': status,
      'totalAmount': 1500000,
      'versionNo': '1',
      'signedOnline': online,
      'paperEvidenceStatus': paper,
    };

Map<String, Object?> evidence(String ocr) => {
      'evidenceId': '77',
      'versionId': '5',
      'versionNo': '1',
      'status': 'PendingReview',
      'signDate': '2026-05-20',
      'fileName': 'ban-ky.pdf',
      'contentType': 'application/pdf',
      'fileSizeBytes': '2048',
      'pageCount': 2,
      'uploadedBy': {'employeeId': '9', 'displayName': 'Nhân viên A', 'roleName': null},
      'uploadedAt': '2026-05-21T02:00:00Z',
      'keywords': {'status': ocr, 'items': <Object>[]},
      'contentChecks': ocr == 'Done'
          ? [
              {'field': 'ContractCode', 'expected': 'HD-1', 'found': ['HD-1'], 'result': 'Match'},
              {'field': 'TaxCode', 'expected': '012', 'found': <String>[], 'result': 'NotFound'},
            ]
          : <Object>[],
    };

Map<String, Object?> overview({bool canUpload = true, List<Map<String, Object?>> evidences = const []}) => {
      'documentId': '1001',
      'documentCode': 'HD-1',
      'documentName': 'Hợp đồng thương mại',
      'contractStatus': 'PendingSignature',
      'customerName': 'Công ty A',
      'totalAmount': 1500000,
      'currentVersion': {'versionId': '5', 'versionNo': '1', 'isLocked': true, 'signDate': null},
      'canUpload': canUpload,
      'uploadBlockedReason': canUpload ? null : 'Đang có một bản ký chờ kiểm tra. Hãy xác nhận hoặc từ chối bản đó trước.',
      'evidences': evidences,
    };

/// Answers the routes of the internal API from a map the test can change.
class _Server {
  final List<http.Request> requests = [];
  final Map<String, http.Response Function(http.Request)> routes = {};

  http.Response ok(Object body) => http.Response.bytes(utf8.encode(jsonEncode(body)), 200);

  _Server() {
    routes['GET /auth/csrf'] = (_) => ok({'token': 'csrf-1'});
    routes['GET /scan-access'] = (_) => ok({
          'employeeId': '9',
          'username': 'a',
          'displayName': 'A',
          'permissions': [ScanAccess.viewPermission, ScanAccess.uploadPermission],
        });
  }

  InternalApiClient client() => InternalApiClient(MockClient((request) async {
        requests.add(request);
        final path = request.url.path.replaceFirst('/api/internal', '');
        final handler = routes['${request.method} $path'];
        if (handler == null) throw StateError('No route for ${request.method} $path');
        return handler(request);
      }));
}

final access = ScanAccess(
  employeeId: '9',
  username: 'a',
  displayName: 'A',
  permissions: {ScanAccess.viewPermission, ScanAccess.uploadPermission},
);

Widget app(Widget home) => MaterialApp(
      home: home,
      routes: {
        '/scans/login': (_) => const ScanLoginPage(),
        '/scans/contracts': (_) => const SignedContractListPage(),
      },
    );

const documentPath = '/contract-documents/1001/signed-evidence';

/// Every screen is laid out at the width of a small phone.
void usePhone(WidgetTester tester, {double height = 800}) {
  tester.view.physicalSize = Size(360, height);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
}

/// Lets the toast timers finish and disposes the page and its OCR poll.
Future<void> finish(WidgetTester tester) async {
  await tester.pumpWidget(const SizedBox());
  await tester.pump(const Duration(seconds: 5));
}

void main() {
  late _Server server;
  late _FakePicker picker;

  setUp(() {
    SharedPreferences.setMockInitialValues({});
    dotenv.loadFromString(envString: 'API_INTERNAL_BASE_URL=http://api.test');
    server = _Server();
    InternalApiClient.instance = server.client();
    picker = _FakePicker();
    FilePickerPlatform.instance = picker;
  });

  testWidgets('the list starts on contracts still waiting for a signed copy', (tester) async {
    usePhone(tester, height: 1400);
    server.routes['GET /contract-documents'] = (_) => server.ok([
          item('1', 'HD-CHO'),
          item('2', 'HD-DANG-KIEM', paper: 'PendingReview'),
          item('3', 'HD-TU-CHOI', paper: 'Rejected'),
          item('4', 'HD-ONLINE', online: true),
          item('5', 'HD-DA-KY', status: 'Signed', paper: 'Accepted'),
        ]);

    await tester.pumpWidget(app(const SignedContractListPage()));
    await tester.pumpAndSettle();

    expect(find.text('HD-CHO'), findsOneWidget);
    expect(find.text('HD-TU-CHOI'), findsOneWidget);
    expect(find.text('HD-DANG-KIEM'), findsNothing);
    expect(find.text('HD-ONLINE'), findsNothing);

    await tester.tap(find.text('Tất cả'));
    await tester.pumpAndSettle();
    expect(find.text('HD-DA-KY'), findsOneWidget);
    expect(find.text('Hợp lệ'), findsOneWidget);

    await tester.enterText(find.byType(TextField), 'công ty hd-online');
    await tester.pumpAndSettle();
    expect(find.text('HD-ONLINE'), findsOneWidget);
    expect(find.text('HD-CHO'), findsNothing);
    await finish(tester);
  });

  testWidgets('login opens the list and shows the server message on failure', (tester) async {
    usePhone(tester);
    var signedIn = false;
    final access = server.routes['GET /scan-access']!;
    server.routes['GET /scan-access'] = (request) => signedIn
        ? access(request)
        : http.Response.bytes(utf8.encode('{"message":"Vui lòng đăng nhập."}'), 401);
    server.routes['GET /contract-documents'] = (_) => server.ok([item('1', 'HD-CHO')]);
    var wrong = true;
    server.routes['POST /auth/login'] = (request) {
      if (wrong) return http.Response.bytes(utf8.encode('{"message":"Tên đăng nhập hoặc mật khẩu không đúng."}'), 401);
      signedIn = true;
      return server.ok({'id': '9'});
    };

    await tester.pumpWidget(app(const ScanLoginPage()));
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField).at(0), 'ao_test');
    await tester.enterText(find.byType(TextField).at(1), 'sai');
    await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
    await tester.pumpAndSettle();
    expect(find.text('Tên đăng nhập hoặc mật khẩu không đúng.'), findsOneWidget);

    wrong = false;
    await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
    await tester.pumpAndSettle();

    final login = server.requests.lastWhere((r) => r.url.path.endsWith('/auth/login'));
    expect(login.headers['X-CSRF-TOKEN'], 'csrf-1');
    expect(jsonDecode(login.body), {'username': 'ao_test', 'password': 'sai'});
    expect(find.text('HD-CHO'), findsOneWidget);
    await finish(tester);
  });

  testWidgets('an employee without the view permission is told so', (tester) async {
    usePhone(tester);
    var calls = 0;
    server.routes['GET /scan-access'] = (_) => ++calls == 1
        ? http.Response.bytes(utf8.encode('{"message":"x"}'), 401)
        : server.ok({'employeeId': '9', 'username': 'a', 'displayName': 'A', 'permissions': <String>[]});
    server.routes['POST /auth/login'] = (_) => server.ok({'id': '9'});
    server.routes['POST /auth/logout'] = (_) => http.Response('', 204);

    await tester.pumpWidget(app(const ScanLoginPage()));
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).at(0), 'a');
    await tester.enterText(find.byType(TextField).at(1), 'b');
    await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
    await tester.pumpAndSettle();

    expect(find.textContaining('chưa được cấp quyền'), findsOneWidget);
    expect(server.requests.where((r) => r.url.path.endsWith('/auth/logout')), hasLength(1));
    await finish(tester);
  });

  testWidgets('the detail page follows OCR until it finishes, then stops asking', (tester) async {
    usePhone(tester);
    var ocr = 'Processing';
    server.routes['GET $documentPath'] = (_) => server.ok(overview(canUpload: false, evidences: [evidence(ocr)]));

    await tester.pumpWidget(app(SignedContractDetailPage(documentId: '1001', access: access)));
    await tester.pump();
    await tester.pump();

    expect(find.text('Đang đọc bản scan…'), findsOneWidget);
    expect(find.text('Tải bản ký lên'), findsNothing);
    expect(find.textContaining('chờ kiểm tra. Hãy xác nhận'), findsOneWidget);

    ocr = 'Done';
    await tester.pump(const Duration(seconds: 3));
    await tester.pump();
    expect(find.text('Khớp'), findsOneWidget);
    expect(find.text('Không thấy'), findsOneWidget);

    final asked = server.requests.length;
    await tester.pump(const Duration(seconds: 10));
    expect(server.requests.length, asked);
    await finish(tester);
  });

  testWidgets('a view-only employee gets no upload button', (tester) async {
    usePhone(tester);
    server.routes['GET $documentPath'] = (_) => server.ok(overview());
    final viewOnly = ScanAccess(employeeId: '9', username: 'a', displayName: 'A', permissions: {ScanAccess.viewPermission});

    await tester.pumpWidget(app(SignedContractDetailPage(documentId: '1001', access: viewOnly)));
    await tester.pumpAndSettle();

    expect(find.text('Tải bản ký lên'), findsNothing);
    expect(find.textContaining('chỉ có quyền xem'), findsOneWidget);
    await finish(tester);
  });

  Future<void> openCaptureWithPdf(WidgetTester tester) async {
    server.routes['GET $documentPath'] = (_) => server.ok(overview());
    await tester.pumpWidget(app(SignedContractDetailPage(documentId: '1001', access: access)));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tải bản ký lên'));
    await tester.pumpAndSettle();

    picker.next = [_PickedFile('may-scan.pdf', Uint8List.fromList(utf8.encode('%PDF-1.4 scan')))];
    await tester.tap(find.text('Chọn PDF / ảnh'));
    await tester.pumpAndSettle();
    expect(find.text('may-scan.pdf'), findsOneWidget);
  }

  Future<void> chooseTodayAndConfirm(WidgetTester tester) async {
    await tester.tap(find.text('Chọn ngày ký'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Chọn'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên'));
    await tester.pumpAndSettle();
  }

  testWidgets('a scan needs its sign date, then uploads and comes back reading', (tester) async {
    usePhone(tester);
    await openCaptureWithPdf(tester);

    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên'));
    await tester.pumpAndSettle();
    expect(find.text('Hãy chọn ngày ký ghi trên tài liệu.'), findsOneWidget);

    server.routes['POST $documentPath'] = (_) => server.ok(overview(canUpload: false, evidences: [evidence('Pending')]));
    await chooseTodayAndConfirm(tester);
    expect(find.text('Tải bản ký lên?'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên').last);
    await tester.pump();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));

    final upload = server.requests.lastWhere((r) => r.method == 'POST' && r.url.path.endsWith('signed-evidence'));
    final body = latin1.decode(upload.bodyBytes);
    expect(upload.headers['X-CSRF-TOKEN'], 'csrf-1');
    expect(body, contains('name="versionId"'));
    expect(body, contains('name="signDate"'));
    expect(body, contains('filename="may-scan.pdf"'));
    expect(find.text('Đang chờ đọc bản scan…'), findsOneWidget);
    await finish(tester);
  });

  testWidgets('a dropped upload keeps the pages for another try', (tester) async {
    usePhone(tester);
    await openCaptureWithPdf(tester);
    var attempts = 0;
    final inner = InternalApiClient(MockClient((request) async {
      final path = request.url.path.replaceFirst('/api/internal', '');
      if (request.method == 'POST' && ++attempts == 1) throw http.ClientException('network down');
      if (request.method == 'POST') return server.ok(overview(canUpload: false, evidences: [evidence('Pending')]));
      if (path == '/auth/csrf') return server.ok({'token': 'csrf-1'});
      return server.ok(overview());
    }));
    InternalApiClient.instance = inner;

    await chooseTodayAndConfirm(tester);
    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên').last);
    await tester.pumpAndSettle();

    expect(find.textContaining('Không kết nối được máy chủ'), findsOneWidget);
    expect(find.text('may-scan.pdf'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên').last);
    await tester.pump();
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 400));

    expect(attempts, 2);
    expect(find.text('Đang chờ đọc bản scan…'), findsOneWidget);
    await finish(tester);
  });

  testWidgets('an expired session asks to sign in again and keeps the scan', (tester) async {
    usePhone(tester);
    await openCaptureWithPdf(tester);
    server.routes['POST $documentPath'] = (_) => http.Response.bytes(utf8.encode('{"message":"Vui lòng đăng nhập."}'), 401);
    server.routes['POST /auth/login'] = (_) => server.ok({'id': '9'});

    await chooseTodayAndConfirm(tester);
    await tester.tap(find.widgetWithText(FilledButton, 'Tải lên').last);
    await tester.pumpAndSettle();
    expect(find.text('Đăng nhập bản ký giấy'), findsOneWidget);

    await tester.enterText(find.byType(TextField).at(0), 'a');
    await tester.enterText(find.byType(TextField).at(1), 'b');
    await tester.tap(find.widgetWithText(FilledButton, 'Đăng nhập'));
    await tester.pumpAndSettle();

    expect(find.text('may-scan.pdf'), findsOneWidget);
    await finish(tester);
  });

  test('pages are joined into one PDF', () async {
    final page = Uint8List.fromList(img.encodePng(img.Image(width: 400, height: 600)));
    final pdf = await ScanPdfBuilder.build([page, page]);
    expect(latin1.decode(pdf.sublist(0, 5)), '%PDF-');
  });
}
