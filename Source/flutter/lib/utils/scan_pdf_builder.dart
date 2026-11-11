import 'dart:typed_data';
import 'package:image/image.dart' as img;
import 'package:pdf/pdf.dart';
import 'package:pdf/widgets.dart' as pw;

/// Ghép ảnh các trang bản scan thành một PDF A4: server nhận đúng một file và tối đa 20 MB.
class ScanPdfBuilder {
  /// Giới hạn của server cho bản scan đã ký.
  static const int maxBytes = 20 * 1024 * 1024;

  /// Cạnh dài tối đa và chất lượng JPEG của từng lượt. Lượt đầu giữ khoảng 300 DPI
  /// trên khổ A4 (3508 px) để OCR đọc tốt; các lượt sau chỉ dùng khi PDF quá lớn.
  static const List<(int, int)> passes = [(3508, 85), (2480, 75), (1754, 65)];

  /// Giải mã ảnh 12 MP tốn vài trăm ms mỗi trang: gọi qua `compute(buildScanPdf, pages)`.
  static Future<Uint8List> build(
    List<Uint8List> pages, {
    int maxBytes = ScanPdfBuilder.maxBytes,
  }) async {
    if (pages.isEmpty) {
      throw ArgumentError('Chưa có trang nào để tạo PDF.');
    }

    for (final (maxSide, quality) in passes) {
      final document = pw.Document(compress: true);
      for (final page in pages) {
        final image = pw.MemoryImage(toJpeg(page, maxSide: maxSide, quality: quality));
        document.addPage(
          pw.Page(
            pageFormat: PdfPageFormat.a4,
            margin: pw.EdgeInsets.zero,
            build: (_) => pw.Center(child: pw.Image(image, fit: pw.BoxFit.contain)),
          ),
        );
      }

      final pdf = await document.save();
      if (pdf.length <= maxBytes) return pdf;
    }

    throw ScanTooLargeException(maxBytes);
  }

  /// Xoay ảnh theo EXIF (ảnh chụp điện thoại thường nằm ngang), thu nhỏ và nén JPEG.
  static Uint8List toJpeg(
    Uint8List bytes, {
    required int maxSide,
    required int quality,
  }) {
    final decoded = img.decodeImage(bytes);
    if (decoded == null) {
      throw const FormatException('Không đọc được ảnh trang scan.');
    }

    var image = img.bakeOrientation(decoded);
    if (image.width > maxSide || image.height > maxSide) {
      image = image.width >= image.height
          ? img.copyResize(image, width: maxSide)
          : img.copyResize(image, height: maxSide);
    }
    return img.encodeJpg(image, quality: quality);
  }
}

/// Top-level so it can run in an isolate with `compute`.
Future<Uint8List> buildScanPdf(List<Uint8List> pages) => ScanPdfBuilder.build(pages);

class ScanTooLargeException implements Exception {
  final int maxBytes;
  const ScanTooLargeException(this.maxBytes);

  @override
  String toString() =>
      'Bản scan vượt quá ${maxBytes ~/ (1024 * 1024)} MB kể cả khi đã nén. Hãy bớt số trang.';
}
