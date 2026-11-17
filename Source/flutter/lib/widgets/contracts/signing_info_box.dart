import 'package:flutter/material.dart';

/// Khung thông báo một dòng có biểu tượng, tô theo màu trạng thái.
class SigningInfoBox extends StatelessWidget {
  final IconData icon;
  final Color color;
  final String text;

  const SigningInfoBox({
    super.key,
    required this.icon,
    required this.color,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: color.withValues(alpha: 0.3)),
      ),
      child: Row(
        children: [
          Icon(icon, color: color),
          const SizedBox(width: 10),
          Expanded(child: Text(text)),
        ],
      ),
    );
  }
}
