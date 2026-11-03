import 'package:flutter/material.dart';
import '../../models/support/ticket_dto.dart';
import '../../services/support/ticket_api.dart';
import '../../theme/app_colors.dart';
import '../../theme/app_theme.dart';
import '../../utils/app_toast.dart';

class TicketDetailPage extends StatefulWidget {
  final TicketModel ticket;

  const TicketDetailPage({super.key, required this.ticket});

  @override
  State<TicketDetailPage> createState() => _TicketDetailPageState();
}

class _TicketDetailPageState extends State<TicketDetailPage> {
  late TicketModel _ticket;
  final TextEditingController _replyController = TextEditingController();
  final ScrollController _scrollController = ScrollController();
  bool _isSending = false;

  @override
  void initState() {
    super.initState();
    _ticket = widget.ticket;
  }

  @override
  void dispose() {
    _replyController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  Future<void> _handleSendReply() async {
    final text = _replyController.text.trim();
    if (text.isEmpty) return;

    setState(() => _isSending = true);
    try {
      final msg = await TicketApi.sendMessage(_ticket.ticketSupportId, text);
      _replyController.clear();
      setState(() {
        _ticket.messages.add(msg);
      });
      AppToast.success(context, 'Đã gửi phản hồi');
      Future.delayed(const Duration(milliseconds: 100), () {
        if (_scrollController.hasClients) {
          _scrollController.animateTo(
            _scrollController.position.maxScrollExtent,
            duration: const Duration(milliseconds: 300),
            curve: Curves.easeOut,
          );
        }
      });
    } catch (_) {
      AppToast.error(context, 'Không thể gửi phản hồi. Vui lòng thử lại.');
    } finally {
      if (mounted) setState(() => _isSending = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final primaryColor = theme.colorScheme.primary;

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              _ticket.ticketCode,
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
            ),
            Text(
              _ticket.department ?? 'Hỗ trợ kỹ thuật',
              style: TextStyle(
                fontSize: 12,
                color: theme.colorScheme.onSurface.withValues(alpha: 0.6),
              ),
            ),
          ],
        ),
        actions: [
          Container(
            margin: const EdgeInsets.only(right: 16),
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
            decoration: BoxDecoration(
              color: _ticket.statusColor.withValues(alpha: 0.12),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: _ticket.statusColor.withValues(alpha: 0.3)),
            ),
            child: Text(
              _ticket.statusEnum.label,
              style: TextStyle(
                fontSize: 11,
                fontWeight: FontWeight.bold,
                color: _ticket.statusColor,
              ),
            ),
          ),
        ],
      ),
      body: Column(
        children: [
          // Info summary banner
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: theme.colorScheme.surface,
              border: Border(
                bottom: BorderSide(color: theme.colorScheme.outline.withValues(alpha: 0.2)),
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                      decoration: BoxDecoration(
                        color: _ticket.priorityColor.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(4),
                      ),
                      child: Text(
                        'Độ ưu tiên: ${_ticket.priorityEnum.label}',
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.bold,
                          color: _ticket.priorityColor,
                        ),
                      ),
                    ),
                    if (_ticket.assignedEmployeeName != null) ...[
                      const SizedBox(width: 8),
                      Text(
                        '•  Phụ trách: ${_ticket.assignedEmployeeName}',
                        style: TextStyle(
                          fontSize: 11,
                          color: theme.colorScheme.onSurface.withValues(alpha: 0.7),
                        ),
                      ),
                    ],
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  _ticket.ticketSubject,
                  style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                if (_ticket.ticketResolution != null) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: AppColors.statusSuccess.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: AppColors.statusSuccess.withValues(alpha: 0.2)),
                    ),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        const Icon(Icons.check_circle_rounded, color: AppColors.statusSuccess, size: 18),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'Kết quả xử lý: ${_ticket.ticketResolution!}',
                            style: const TextStyle(fontSize: 12, color: AppColors.statusSuccess, fontWeight: FontWeight.w500),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),

          // Message Thread List
          Expanded(
            child: ListView.separated(
              controller: _scrollController,
              padding: const EdgeInsets.all(16),
              itemCount: _ticket.messages.length,
              separatorBuilder: (_, __) => const SizedBox(height: 16),
              itemBuilder: (context, index) {
                final msg = _ticket.messages[index];
                return _buildMessageItem(context, msg);
              },
            ),
          ),

          // Reply Input Bar
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            decoration: BoxDecoration(
              color: theme.colorScheme.surface,
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withValues(alpha: 0.05),
                  blurRadius: 10,
                  offset: const Offset(0, -2),
                ),
              ],
            ),
            child: SafeArea(
              child: Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _replyController,
                      decoration: InputDecoration(
                        hintText: 'Nhập câu hỏi hoặc phản hồi...',
                        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(24),
                          borderSide: BorderSide(color: theme.colorScheme.outline.withValues(alpha: 0.3)),
                        ),
                        filled: true,
                        fillColor: theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.4),
                      ),
                      minLines: 1,
                      maxLines: 4,
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    onPressed: _isSending ? null : _handleSendReply,
                    style: IconButton.styleFrom(
                      backgroundColor: primaryColor,
                      foregroundColor: Colors.white,
                    ),
                    icon: _isSending
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                          )
                        : const Icon(Icons.send_rounded, size: 18),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMessageItem(BuildContext context, TicketMessageModel msg) {
    final theme = Theme.of(context);
    final isEmployee = msg.isFromEmployee;
    final primaryColor = theme.colorScheme.primary;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisAlignment: isEmployee ? MainAxisAlignment.start : MainAxisAlignment.end,
      children: [
        if (isEmployee) ...[
          CircleAvatar(
            radius: 16,
            backgroundColor: primaryColor.withValues(alpha: 0.15),
            child: Icon(Icons.support_agent_rounded, size: 18, color: primaryColor),
          ),
          const SizedBox(width: 8),
        ],
        Flexible(
          child: Container(
            constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.78),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: isEmployee
                  ? theme.colorScheme.surfaceContainerHighest.withValues(alpha: 0.6)
                  : primaryColor,
              borderRadius: BorderRadius.only(
                topLeft: const Radius.circular(16),
                topRight: const Radius.circular(16),
                bottomLeft: Radius.circular(isEmployee ? 4 : 16),
                bottomRight: Radius.circular(isEmployee ? 16 : 4),
              ),
              border: isEmployee ? Border.all(color: theme.colorScheme.outline.withValues(alpha: 0.2)) : null,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      msg.senderName,
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                        color: isEmployee ? theme.colorScheme.onSurface : Colors.white,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      '${msg.createdAt.hour.toString().padLeft(2, '0')}:${msg.createdAt.minute.toString().padLeft(2, '0')}',
                      style: TextStyle(
                        fontSize: 10,
                        color: isEmployee
                            ? theme.colorScheme.onSurface.withValues(alpha: 0.5)
                            : Colors.white.withValues(alpha: 0.7),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 6),
                Text(
                  msg.ticketMessageContent,
                  style: TextStyle(
                    fontSize: 13,
                    height: 1.4,
                    color: isEmployee ? theme.colorScheme.onSurface : Colors.white,
                  ),
                ),
                if (msg.attachmentName != null) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: isEmployee
                          ? Colors.black.withValues(alpha: 0.05)
                          : Colors.white.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.attach_file_rounded,
                          size: 14,
                          color: isEmployee ? theme.colorScheme.onSurface : Colors.white,
                        ),
                        const SizedBox(width: 4),
                        Flexible(
                          child: Text(
                            msg.attachmentName!,
                            style: TextStyle(
                              fontSize: 11,
                              color: isEmployee ? theme.colorScheme.onSurface : Colors.white,
                            ),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
        if (!isEmployee) ...[
          const SizedBox(width: 8),
          CircleAvatar(
            radius: 16,
            backgroundColor: theme.colorScheme.surfaceContainerHighest,
            child: Icon(Icons.person_rounded, size: 18, color: theme.colorScheme.onSurfaceVariant),
          ),
        ],
      ],
    );
  }
}
