import '../../models/support/ticket_dto.dart';
import '../api_client.dart';

class TicketApi {
  static final ApiClient _apiClient = ApiClient();
  static const String _baseUrl = '/support/tickets';

  static final List<TicketModel> _mockTickets = [];

  /// Lấy danh sách ticket (có lọc theo trạng thái, từ khóa)
  static Future<List<TicketModel>> getTickets({String? status, String? keyword}) async {
    try {
      final query = <String, String>{};
      if (status != null && status != 'All') query['status'] = status;
      if (keyword != null && keyword.isNotEmpty) query['keyword'] = keyword;

      final qs = Uri(queryParameters: query).query;
      final endpoint = qs.isNotEmpty ? '$_baseUrl?$qs' : _baseUrl;
      final res = await _apiClient.get(endpoint);
      if (res is List) {
        return res.map((e) => TicketModel.fromJson(e as Map<String, dynamic>)).toList();
      }
    } catch (_) {
      // Fallback in-memory
    }

    var list = List<TicketModel>.from(_mockTickets);
    if (status != null && status != 'All') {
      list = list.where((t) => t.ticketStatus.toLowerCase() == status.toLowerCase()).toList();
    }
    if (keyword != null && keyword.trim().isNotEmpty) {
      final kw = keyword.trim().toLowerCase();
      list = list.where((t) =>
          t.ticketCode.toLowerCase().contains(kw) ||
          t.ticketSubject.toLowerCase().contains(kw) ||
          t.ticketDescription.toLowerCase().contains(kw)).toList();
    }
    return list;
  }

  /// Lấy chi tiết ticket theo ID
  static Future<TicketModel?> getTicketById(int ticketId) async {
    try {
      final res = await _apiClient.get('$_baseUrl/$ticketId');
      return TicketModel.fromJson(res as Map<String, dynamic>);
    } catch (_) {
      for (final t in _mockTickets) {
        if (t.ticketSupportId == ticketId) return t;
      }
      return null;
    }
  }

  /// Tạo mới ticket
  static Future<TicketModel> createTicket({
    required String subject,
    required String description,
    required String priority,
    required String department,
    String? module,
    List<String>? attachmentNames,
  }) async {
    final newId = _mockTickets.length + 1;
    final code = 'TK-${DateTime.now().millisecondsSinceEpoch.toString().substring(5)}';

    final initialMessage = TicketMessageModel(
      ticketMessageId: newId * 100 + 1,
      ticketSupportId: newId,
      senderName: 'Khách hàng',
      isFromEmployee: false,
      ticketMessageContent: description,
      attachmentName: (attachmentNames != null && attachmentNames.isNotEmpty)
          ? attachmentNames.join(', ')
          : null,
      createdAt: DateTime.now(),
    );

    final created = TicketModel(
      ticketSupportId: newId,
      ticketCode: code,
      ticketSubject: subject,
      ticketDescription: description,
      ticketPriority: priority,
      ticketStatus: 'Open',
      department: department,
      createdAt: DateTime.now(),
      messages: [initialMessage],
    );

    try {
      final res = await _apiClient.post(
        _baseUrl,
        body: {
          'ticketSubject': subject,
          'ticketDescription': description,
          'ticketPriority': priority,
          'department': department,
          if (module != null) 'module': module,
          if (attachmentNames != null && attachmentNames.isNotEmpty)
            'attachmentNames': attachmentNames,
        },
      );
      if (res is Map<String, dynamic>) {
        final serverTicket = TicketModel.fromJson(res);
        _mockTickets.insert(0, serverTicket);
        return serverTicket;
      }
    } catch (_) {}

    _mockTickets.insert(0, created);
    return created;
  }

  /// Gửi thêm tin nhắn trong ticket
  static Future<TicketMessageModel> sendMessage(int ticketId, String content) async {
    final newMsg = TicketMessageModel(
      ticketMessageId: DateTime.now().millisecondsSinceEpoch % 100000,
      ticketSupportId: ticketId,
      senderName: 'Khách hàng',
      isFromEmployee: false,
      ticketMessageContent: content,
      createdAt: DateTime.now(),
    );

    try {
      await _apiClient.post(
        '$_baseUrl/$ticketId/messages',
        body: {'content': content},
      );
    } catch (_) {}

    for (final t in _mockTickets) {
      if (t.ticketSupportId == ticketId) {
        t.messages.add(newMsg);
        break;
      }
    }
    return newMsg;
  }
}
