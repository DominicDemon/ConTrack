import 'dart:convert';
import 'package:dtc_digitizingcontract/services/scans/internal_api_client.dart';
import 'package:flutter_dotenv/flutter_dotenv.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';

http.Response json(Object body, {int status = 200, Map<String, String> headers = const {}}) => http.Response.bytes(
      utf8.encode(jsonEncode(body)),
      status,
      headers: {'content-type': 'application/json', ...headers},
    );

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
    dotenv.loadFromString(envString: 'API_INTERNAL_BASE_URL=http://api.test/');
  });

  test('sends the CSRF token and every cookie the server set', () async {
    final seen = <http.Request>[];
    final client = InternalApiClient(MockClient((request) async {
      seen.add(request);
      if (request.url.path.endsWith('/auth/csrf')) {
        return json({'token': 'abc'}, headers: {
          // dart:io joins several Set-Cookie headers with commas.
          'set-cookie': '.Csrf=c1; path=/; samesite=strict, .Session=s1; expires=Thu, 01 Jan 2099 00:00:00 GMT; path=/',
        });
      }
      return json({'ok': true});
    }));

    await client.postJson('/first', {'a': 1});
    await client.postJson('/second', {'a': 2});

    expect(seen.map((r) => r.url.toString()), [
      'http://api.test/api/internal/auth/csrf',
      'http://api.test/api/internal/first',
      'http://api.test/api/internal/second',
    ]);
    expect(seen[1].headers['X-CSRF-TOKEN'], 'abc');
    expect(seen[1].headers['Cookie'], '.Csrf=c1; .Session=s1');
  });

  test('a dropped connection keeps the session', () async {
    var fail = false;
    String? cookie;
    final client = InternalApiClient(MockClient((request) async {
      if (fail) throw http.ClientException('network down');
      cookie = request.headers['Cookie'];
      return json({'ok': true}, headers: {'set-cookie': '.Session=s1; path=/'});
    }));

    await client.get('/a');
    fail = true;
    await expectLater(
      client.get('/b'),
      throwsA(isA<InternalApiException>().having((e) => e.statusCode, 'statusCode', isNull)),
    );
    fail = false;
    await client.get('/c');

    expect(cookie, '.Session=s1');
  });

  test('a 401 ends the session and keeps the server message', () async {
    final cookies = <String?>[];
    var status = 200;
    final client = InternalApiClient(MockClient((request) async {
      cookies.add(request.headers['Cookie']);
      return status == 200
          ? json({'ok': true}, headers: {'set-cookie': '.Session=s1; path=/'})
          : json({'message': 'Vui lòng đăng nhập.'}, status: status);
    }));

    await client.get('/a');
    status = 401;
    await expectLater(
      client.get('/b'),
      throwsA(isA<InternalApiException>()
          .having((e) => e.isUnauthorized, 'isUnauthorized', isTrue)
          .having((e) => e.message, 'message', 'Vui lòng đăng nhập.')),
    );
    status = 200;
    await client.get('/c');

    expect(cookies, [null, '.Session=s1', null]);
  });

  test('a 400 asks for a new CSRF token next time', () async {
    var csrfCalls = 0;
    var status = 400;
    final client = InternalApiClient(MockClient((request) async {
      if (request.url.path.endsWith('/auth/csrf')) {
        csrfCalls++;
        return json({'token': 't$csrfCalls'});
      }
      return status == 200 ? json({'ok': true}) : json({'message': 'x'}, status: status);
    }));

    await expectLater(client.postJson('/a', {}), throwsA(isA<InternalApiException>()));
    status = 200;
    await client.postJson('/a', {});

    expect(csrfCalls, 2);
  });
}
