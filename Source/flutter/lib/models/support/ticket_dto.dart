import 'package:flutter/material.dart';
import '../../theme/app_colors.dart';

enum TicketStatus {
  open('Open', 'Chờ tiếp nhận'),
  inProgress('In_Progress', 'Đang xử lý'),
  resolved('Resolved', 'Đã xử lý');

  final String value;
  final String label;
  const TicketStatus(this.value, this.label);

  static TicketStatus fromString(String? val) {
    final normalized = (val ?? '').trim().toLowerCase();
    if (normalized == 'in_progress' ||
        normalized == 'inprogress' ||
        normalized == 'pending_customer') {
      return TicketStatus.inProgress;
    }
    if (normalized == 'resolved' ||
        normalized == 'closed' ||
        normalized == 'completed') {
      return TicketStatus.resolved;
    }
    return TicketStatus.open;
  }
}

enum TicketPriority {
  low('Low', 'Thấp'),
  medium('Medium', 'Bình thường'),
  high('High', 'Cao'),
  urgent('Urgent', 'Khẩn cấp');

  final String value;
  final String label;
  const TicketPriority(this.value, this.label);

  static TicketPriority fromString(String? val) {
    return TicketPriority.values.firstWhere(
      (e) => e.value.toLowerCase() == (val ?? '').toLowerCase(),
      orElse: () => TicketPriority.medium,
    );
  }
}

class TicketMessageModel {
  final int ticketMessageId;
  final int ticketSupportId;
  final int? senderCustomerId;
  final int? senderEmployeeId;
  final String senderName;
  final bool isFromEmployee;
  final String ticketMessageContent;
  final String ticketMessageType;
  final String? attachmentUrl;
  final String? attachmentName;
  final DateTime createdAt;

  TicketMessageModel({
    required this.ticketMessageId,
    required this.ticketSupportId,
    this.senderCustomerId,
    this.senderEmployeeId,
    required this.senderName,
    required this.isFromEmployee,
    required this.ticketMessageContent,
    this.ticketMessageType = 'TEXT',
    this.attachmentUrl,
    this.attachmentName,
    required this.createdAt,
  });

  factory TicketMessageModel.fromJson(Map<String, dynamic> json) {
    return TicketMessageModel(
      ticketMessageId: json['ticketMessageId'] ?? json['ticket_message_id'] ?? 0,
      ticketSupportId: json['ticketSupportId'] ?? json['ticket_support_id'] ?? 0,
      senderCustomerId: json['senderCustomerId'] ?? json['sender_customer_id'],
      senderEmployeeId: json['senderEmployeeId'] ?? json['sender_employee_id'],
      senderName: json['senderName'] ?? (json['senderEmployeeId'] != null ? 'Kỹ thuật viên' : 'Khách hàng'),
      isFromEmployee: json['isFromEmployee'] ?? (json['senderEmployeeId'] != null),
      ticketMessageContent: json['ticketMessageContent'] ?? json['ticket_message_content'] ?? '',
      ticketMessageType: json['ticketMessageType'] ?? json['ticket_message_type'] ?? 'TEXT',
      attachmentUrl: json['attachmentUrl'] ?? json['ticket_message_attachment_url'],
      attachmentName: json['attachmentName'] ?? json['ticket_message_attachment_name'],
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'].toString()) ?? DateTime.now()
          : DateTime.now(),
    );
  }

  Map<String, dynamic> toJson() => {
    'ticketMessageId': ticketMessageId,
    'ticketSupportId': ticketSupportId,
    'ticketMessageContent': ticketMessageContent,
    'ticketMessageType': ticketMessageType,
    'attachmentUrl': attachmentUrl,
    'attachmentName': attachmentName,
  };
}

class TicketModel {
  final int ticketSupportId;
  final int? customerId;
  final int? tenantId;
  final String ticketCode;
  final String ticketSubject;
  final String ticketDescription;
  final String ticketPriority;
  final String ticketStatus;
  final String? department;
  final String? ticketResolution;
  final DateTime? ticketResolvedAt;
  final DateTime? ticketClosedAt;
  final DateTime createdAt;
  final DateTime? updatedAt;
  final String? assignedEmployeeName;
  final List<TicketMessageModel> messages;

  TicketModel({
    required this.ticketSupportId,
    this.customerId,
    this.tenantId,
    required this.ticketCode,
    required this.ticketSubject,
    required this.ticketDescription,
    this.ticketPriority = 'Medium',
    this.ticketStatus = 'Open',
    this.department,
    this.ticketResolution,
    this.ticketResolvedAt,
    this.ticketClosedAt,
    required this.createdAt,
    this.updatedAt,
    this.assignedEmployeeName,
    this.messages = const [],
  });

  TicketStatus get statusEnum => TicketStatus.fromString(ticketStatus);
  TicketPriority get priorityEnum => TicketPriority.fromString(ticketPriority);

  Color get statusColor {
    switch (statusEnum) {
      case TicketStatus.open:
        return AppColors.statusWarning;
      case TicketStatus.inProgress:
        return AppColors.statusInfo;
      case TicketStatus.resolved:
        return AppColors.statusSuccess;
    }
  }

  Color get priorityColor {
    switch (priorityEnum) {
      case TicketPriority.urgent:
        return AppColors.lightDestructive;
      case TicketPriority.high:
        return AppColors.statusWarning;
      case TicketPriority.medium:
        return AppColors.lightPrimary;
      case TicketPriority.low:
        return AppColors.lightMutedForeground;
    }
  }

  factory TicketModel.fromJson(Map<String, dynamic> json) {
    return TicketModel(
      ticketSupportId: json['ticketSupportId'] ?? json['ticket_support_id'] ?? 0,
      customerId: json['customerId'] ?? json['customer_id'],
      tenantId: json['tenantId'] ?? json['tenant_id'],
      ticketCode: json['ticketCode'] ?? json['ticket_code'] ?? '',
      ticketSubject: json['ticketSubject'] ?? json['ticket_subject'] ?? '',
      ticketDescription: json['ticketDescription'] ?? json['ticket_description'] ?? '',
      ticketPriority: json['ticketPriority'] ?? json['ticket_priority'] ?? 'Medium',
      ticketStatus: json['ticketStatus'] ?? json['ticket_status'] ?? 'Open',
      department: json['department'],
      ticketResolution: json['ticketResolution'] ?? json['ticket_resolution'],
      ticketResolvedAt: json['ticketResolvedAt'] != null
          ? DateTime.tryParse(json['ticketResolvedAt'].toString())
          : null,
      ticketClosedAt: json['ticketClosedAt'] != null
          ? DateTime.tryParse(json['ticketClosedAt'].toString())
          : null,
      createdAt: json['createdAt'] != null
          ? DateTime.tryParse(json['createdAt'].toString()) ?? DateTime.now()
          : DateTime.now(),
      updatedAt: json['updatedAt'] != null
          ? DateTime.tryParse(json['updatedAt'].toString())
          : null,
      assignedEmployeeName: json['assignedEmployeeName'],
      messages: (json['messages'] as List<dynamic>?)
              ?.map((e) => TicketMessageModel.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }
}
