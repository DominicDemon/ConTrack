class AppFormatUtils {
  /// 1500000000 → "1.500.000.000 VND"; giữ 2 chữ số thập phân khi có (vd. USD).
  static String formatMoney(double amount, String currencyCode) {
    final negative = amount < 0;
    final cents = (amount.abs() * 100).round();
    final whole = (cents ~/ 100).toString();
    final fraction = cents % 100;

    final grouped = StringBuffer();
    for (var i = 0; i < whole.length; i++) {
      if (i > 0 && (whole.length - i) % 3 == 0) grouped.write('.');
      grouped.write(whole[i]);
    }
    final decimals = fraction == 0 ? '' : ',${fraction.toString().padLeft(2, '0')}';
    return '${negative ? '-' : ''}$grouped$decimals $currencyCode';
  }

  /// 2048 → "2 KB", 3355443 → "3.2 MB".
  static String formatFileSize(int bytes) {
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).round()} KB';
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }

  /// dd/MM/yyyy theo giờ máy.
  static String formatDate(DateTime? date) {
    if (date == null) return '—';
    final local = date.toLocal();
    return '${local.day.toString().padLeft(2, '0')}/'
        '${local.month.toString().padLeft(2, '0')}/${local.year}';
  }

  /// dd/MM/yyyy HH:mm theo giờ máy.
  static String formatDateTime(DateTime? date) {
    if (date == null) return '—';
    final local = date.toLocal();
    return '${formatDate(local)} ${local.hour.toString().padLeft(2, '0')}:'
        '${local.minute.toString().padLeft(2, '0')}';
  }
}
