import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../services/support/ticket_api.dart';
import '../../theme/app_colors.dart';
import '../../theme/app_theme.dart';
import '../../utils/app_toast.dart';

class _AttachedFileItem {
  final String name;
  final String? localPath;

  const _AttachedFileItem({
    required this.name,
    this.localPath,
  });

  bool get isImage {
    final lower = name.toLowerCase();
    return lower.endsWith('.png') ||
        lower.endsWith('.jpg') ||
        lower.endsWith('.jpeg') ||
        lower.endsWith('.webp');
  }
}

class CreateTicketPage extends StatefulWidget {
  const CreateTicketPage({super.key});

  @override
  State<CreateTicketPage> createState() => _CreateTicketPageState();
}

class _CreateTicketPageState extends State<CreateTicketPage> {
  static const MethodChannel _filePickerChannel =
      MethodChannel('com.contrack/file_picker');

  final _formKey = GlobalKey<FormState>();
  final TextEditingController _subjectController = TextEditingController();
  final TextEditingController _descriptionController = TextEditingController();

  String _selectedDepartment = 'KỸ THUẬT';
  String _selectedPriority = 'Medium';
  String _selectedModule = 'Ký số & Hợp đồng điện tử';
  bool _isSubmitting = false;

  final List<_AttachedFileItem> _attachedFiles = [];

  final List<Map<String, String>> _departments = [
    {
      'id': 'KỸ THUẬT',
      'title': 'KỸ THUẬT',
      'desc': 'Hướng dẫn & xử lý sự cố hệ thống 24/7/365',
      'icon': 'build',
    },
    {
      'id': 'DỊCH VỤ KHÁCH HÀNG',
      'title': 'DỊCH VỤ KHÁCH HÀNG',
      'desc': 'Chính sách dịch vụ, tài khoản, hướng dẫn sử dụng',
      'icon': 'support_agent',
    },
    {
      'id': 'BÁN HÀNG',
      'title': 'BÁN HÀNG & GÓI CƯỚC',
      'desc': 'Tư vấn tính năng, báo giá, nâng cấp gói hợp đồng',
      'icon': 'shopping_bag',
    },
    {
      'id': 'KẾ TOÁN - HÀNH CHÍNH',
      'title': 'KẾ TOÁN - HÀNH CHÍNH',
      'desc': 'Thanh toán, hóa đơn VAT điện tử, gia hạn',
      'icon': 'receipt_long',
    },
    {
      'id': 'TIẾP NHẬN THAN PHIỀN - GÓP Ý',
      'title': 'THAN PHIỀN - GÓP Ý',
      'desc': 'Phản ánh chất lượng dịch vụ, thái độ nhân viên',
      'icon': 'feedback',
    },
  ];

  final List<Map<String, String>> _modules = [
    {'id': 'Ký số & Hợp đồng điện tử', 'name': 'Ký số & Hợp đồng điện tử'},
    {'id': 'Mẫu hợp đồng & Biến động version', 'name': 'Mẫu hợp đồng & Phiên bản'},
    {'id': 'Bàn giao & Nghiệm thu hợp đồng', 'name': 'Bàn giao & Nghiệm thu'},
    {'id': 'Quản lý Báo giá & Khách hàng', 'name': 'Quản lý Báo giá & Khách hàng'},
    {'id': 'Số hóa & Scan OCR hợp đồng', 'name': 'Số hóa & Scan OCR hợp đồng'},
    {'id': 'Tài khoản & Phân quyền nội bộ', 'name': 'Tài khoản & Phân quyền'},
    {'id': 'Khác', 'name': 'Vấn đề kỹ thuật khác'},
  ];

  final List<Map<String, String>> _priorities = [
    {'value': 'Low', 'label': 'Thấp', 'desc': 'Góp ý nhỏ'},
    {'value': 'Medium', 'label': 'Bình thường', 'desc': 'Cần hỗ trợ'},
    {'value': 'High', 'label': 'Cao', 'desc': 'Ảnh hưởng tiến độ'},
    {'value': 'Urgent', 'label': 'Khẩn cấp', 'desc': 'Dừng luồng ký HĐ'},
  ];

  // Danh sách ảnh / tệp trong thư viện thiết bị (dùng khi máy ảo hoặc nền tảng chưa cấp quyền native)
  final List<Map<String, String>> _deviceGallerySamples = [
    {
      'folder': 'Thư viện Ảnh / Screenshots',
      'name': 'Screenshot_Loi_Ky_So_OTP.png',
      'size': '1.2 MB',
    },
    {
      'folder': 'Thư viện Ảnh / Screenshots',
      'name': 'Screenshot_Khong_Xuat_Duoc_PDF.png',
      'size': '845 KB',
    },
    {
      'folder': 'Thư viện Ảnh / Camera',
      'name': 'IMG_CCCD_Mat_Truoc_Dai_Dien.jpg',
      'size': '2.4 MB',
    },
    {
      'folder': 'Thư viện Ảnh / Camera',
      'name': 'IMG_Giay_Phep_Kinh_Doanh.jpg',
      'size': '3.1 MB',
    },
    {
      'folder': 'Tệp tài liệu / Downloads',
      'name': 'Hop_Dong_Dich_Vu_ConTrack.pdf',
      'size': '1.8 MB',
    },
    {
      'folder': 'Tệp tài liệu / Downloads',
      'name': 'Bien_Ban_Nghiem_Thu_Loi.pdf',
      'size': '620 KB',
    },
  ];

  @override
  void dispose() {
    _subjectController.dispose();
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _openAttachmentPickerSheet() async {
    if (_attachedFiles.length >= 3) {
      AppToast.error(context, 'Tối đa đính kèm 3 tệp minh họa');
      return;
    }

    final theme = Theme.of(context);
    final primaryColor = theme.colorScheme.primary;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: theme.colorScheme.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (sheetContext) {
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 20),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Center(
                  child: Container(
                    width: 40,
                    height: 4,
                    decoration: BoxDecoration(
                      color: theme.colorScheme.outline.withValues(alpha: 0.3),
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
                const SizedBox(height: 16),
                const Text(
                  'Chọn nguồn hình ảnh / tệp đính kèm',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 4),
                Text(
                  'Chọn ảnh từ thư mục ảnh trên thiết bị hoặc duyệt tệp tài liệu minh họa',
                  style: TextStyle(
                    fontSize: 12,
                    color: theme.colorScheme.onSurface.withValues(alpha: 0.65),
                  ),
                ),
                const SizedBox(height: 16),

                // Tùy chọn 1: Mở Thư viện ảnh của hệ điều hành (Android Photo Gallery)
                ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                    side: BorderSide(color: primaryColor.withValues(alpha: 0.3)),
                  ),
                  tileColor: primaryColor.withValues(alpha: 0.05),
                  leading: CircleAvatar(
                    backgroundColor: primaryColor.withValues(alpha: 0.15),
                    child: Icon(Icons.photo_library_rounded, color: primaryColor),
                  ),
                  title: const Text(
                    'Mở Thư viện Ảnh trên máy (Gallery)',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                  subtitle: const Text(
                    'Chọn ảnh chụp màn hình hoặc ảnh từ album trong điện thoại',
                    style: TextStyle(fontSize: 12),
                  ),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () async {
                    Navigator.of(sheetContext).pop();
                    await _pickFromNativeOS('pickImage');
                  },
                ),
                const SizedBox(height: 10),

                // Tùy chọn 2: Mở Trình quản lý tệp (Files / PDF / Ảnh)
                ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                    side: BorderSide(
                      color: theme.colorScheme.outline.withValues(alpha: 0.25),
                    ),
                  ),
                  leading: CircleAvatar(
                    backgroundColor: Colors.blue.withValues(alpha: 0.12),
                    child: const Icon(Icons.folder_open_rounded, color: Colors.blue),
                  ),
                  title: const Text(
                    'Duyệt Thư mục Tệp & Tài liệu (Files / PDF)',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                  subtitle: const Text(
                    'Mở trình quản lý thư mục để chọn file .png, .jpg, .pdf',
                    style: TextStyle(fontSize: 12),
                  ),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () async {
                    Navigator.of(sheetContext).pop();
                    await _pickFromNativeOS('pickDocument');
                  },
                ),
                const SizedBox(height: 10),

                // Tùy chọn 3: Chọn nhanh từ Thư mục Ảnh & Tài liệu trong ứng dụng
                ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                    side: BorderSide(
                      color: theme.colorScheme.outline.withValues(alpha: 0.25),
                    ),
                  ),
                  leading: CircleAvatar(
                    backgroundColor: Colors.teal.withValues(alpha: 0.12),
                    child: const Icon(Icons.collections_rounded, color: Colors.teal),
                  ),
                  title: const Text(
                    'Chọn từ Thư mục Ảnh chụp màn hình & Hồ sơ',
                    style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                  subtitle: const Text(
                    'Xem danh sách thư mục ảnh & tài liệu có sẵn để chọn',
                    style: TextStyle(fontSize: 12),
                  ),
                  trailing: const Icon(Icons.chevron_right_rounded),
                  onTap: () {
                    Navigator.of(sheetContext).pop();
                    _openFolderBrowserDialog();
                  },
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _pickFromNativeOS(String method) async {
    try {
      final result = await _filePickerChannel.invokeMethod<dynamic>(method);
      if (result == null) {
        // Người dùng bấm Hủy trên trình chọn ảnh
        return;
      }
      if (result is Map) {
        final name = (result['name'] ?? 'anh_minh_hoa.jpg').toString();
        final path = result['path']?.toString();

        if (!mounted) return;
        setState(() {
          _attachedFiles.add(_AttachedFileItem(name: name, localPath: path));
        });
        AppToast.success(context, 'Đã chọn tệp: $name');
      }
    } on MissingPluginException {
      // Nếu app chưa rebuild native Kotlin (chỉ hot-reload), mở ngay trình duyệt thư mục ảnh
      if (mounted) {
        _openFolderBrowserDialog();
      }
    } on PlatformException catch (e) {
      if (mounted) {
        AppToast.error(context, e.message ?? 'Không thể mở thư mục ảnh');
      }
    }
  }

  Future<void> _openFolderBrowserDialog() async {
    final theme = Theme.of(context);
    final primaryColor = theme.colorScheme.primary;
    String selectedFolder = 'Tất cả thư mục';

    final folders = [
      'Tất cả thư mục',
      'Thư viện Ảnh / Screenshots',
      'Thư viện Ảnh / Camera',
      'Tệp tài liệu / Downloads',
    ];

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: theme.colorScheme.surface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (dialogContext) {
        return StatefulBuilder(
          builder: (ctx, setModalState) {
            final filteredItems = selectedFolder == 'Tất cả thư mục'
                ? _deviceGallerySamples
                : _deviceGallerySamples
                    .where((item) => item['folder'] == selectedFolder)
                    .toList();

            return SafeArea(
              child: SizedBox(
                height: MediaQuery.of(context).size.height * 0.68,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 14, 16, 8),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                'Thư mục Ảnh & Tệp trên thiết bị',
                                style: TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                              SizedBox(height: 2),
                              Text(
                                'Chạm vào ảnh hoặc tệp bất kỳ để đính kèm vào yêu cầu',
                                style: TextStyle(fontSize: 12, color: Colors.grey),
                              ),
                            ],
                          ),
                          IconButton(
                            onPressed: () => Navigator.of(dialogContext).pop(),
                            icon: const Icon(Icons.close_rounded),
                          ),
                        ],
                      ),
                    ),

                    // Bộ lọc thư mục
                    SingleChildScrollView(
                      scrollDirection: Axis.horizontal,
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                      child: Row(
                        children: folders.map((folder) {
                          final active = selectedFolder == folder;
                          return Padding(
                            padding: const EdgeInsets.only(right: 8),
                            child: ChoiceChip(
                              label: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Icon(
                                    Icons.folder_rounded,
                                    size: 15,
                                    color: active ? primaryColor : Colors.grey,
                                  ),
                                  const SizedBox(width: 6),
                                  Text(
                                    folder,
                                    style: const TextStyle(fontSize: 12),
                                  ),
                                ],
                              ),
                              selected: active,
                              onSelected: (_) {
                                setModalState(() => selectedFolder = folder);
                              },
                            ),
                          );
                        }).toList(),
                      ),
                    ),
                    const Divider(height: 12),

                    Expanded(
                      child: ListView.separated(
                        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                        itemCount: filteredItems.length,
                        separatorBuilder: (_, __) => const SizedBox(height: 8),
                        itemBuilder: (_, index) {
                          final item = filteredItems[index];
                          final name = item['name']!;
                          final isPdf = name.toLowerCase().endsWith('.pdf');
                          final alreadyAdded =
                              _attachedFiles.any((f) => f.name == name);

                          return ListTile(
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12),
                              side: BorderSide(
                                color: alreadyAdded
                                    ? primaryColor
                                    : theme.colorScheme.outline.withValues(alpha: 0.2),
                              ),
                            ),
                            tileColor: alreadyAdded
                                ? primaryColor.withValues(alpha: 0.06)
                                : theme.colorScheme.surface,
                            leading: Container(
                              width: 44,
                              height: 44,
                              decoration: BoxDecoration(
                                color: isPdf
                                    ? Colors.red.withValues(alpha: 0.12)
                                    : primaryColor.withValues(alpha: 0.12),
                                borderRadius: BorderRadius.circular(10),
                              ),
                              child: Icon(
                                isPdf
                                    ? Icons.picture_as_pdf_rounded
                                    : Icons.image_rounded,
                                color: isPdf ? Colors.red : primaryColor,
                              ),
                            ),
                            title: Text(
                              name,
                              style: const TextStyle(
                                fontSize: 13,
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                            subtitle: Text(
                              '${item['folder']} • ${item['size']}',
                              style: const TextStyle(fontSize: 11),
                            ),
                            trailing: alreadyAdded
                                ? Icon(Icons.check_circle_rounded, color: primaryColor)
                                : const Icon(Icons.add_circle_outline_rounded),
                            onTap: () {
                              if (alreadyAdded) {
                                AppToast.error(
                                  context,
                                  'Tệp này đã có trong danh sách đính kèm',
                                );
                                return;
                              }
                              setState(() {
                                _attachedFiles.add(_AttachedFileItem(name: name));
                              });
                              Navigator.of(dialogContext).pop();
                              AppToast.success(context, 'Đã chọn tệp: $name');
                            },
                          );
                        },
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }

  void _removeAttachment(int index) {
    setState(() {
      _attachedFiles.removeAt(index);
    });
  }

  Future<void> _handleSubmit() async {
    if (!_formKey.currentState!.validate()) return;

    setState(() => _isSubmitting = true);
    try {
      await TicketApi.createTicket(
        subject: _subjectController.text.trim(),
        description: _descriptionController.text.trim(),
        priority: _selectedPriority,
        department: _selectedDepartment,
        module: _selectedModule,
        attachmentNames: _attachedFiles.map((f) => f.name).toList(),
      );

      if (mounted) {
        AppToast.success(
          context,
          'Gửi yêu cầu hỗ trợ thành công! Đội ngũ sẽ phản hồi sớm.',
        );
        Navigator.of(context).pop(true);
      }
    } catch (e) {
      if (mounted) {
        AppToast.error(context, 'Không thể gửi yêu cầu. Vui lòng thử lại.');
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final primaryColor = theme.colorScheme.primary;

    return Scaffold(
      appBar: AppBar(
        title: const Text(
          'Gửi yêu cầu hỗ trợ',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 18),
        ),
      ),
      body: Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Notice banner
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: AppColors.lightSecondary,
                  borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                  border: Border.all(color: primaryColor.withValues(alpha: 0.2)),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(Icons.info_outline_rounded, color: primaryColor, size: 20),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'Vui lòng chọn chính xác Phân hệ và Phòng ban để được hỗ trợ nhanh nhất. Hệ thống sẽ kết nối trực tiếp với kỹ thuật viên trực ca.',
                        style: TextStyle(
                          fontSize: 12,
                          height: 1.4,
                          color: theme.colorScheme.onSurface.withValues(alpha: 0.8),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),

              // 1. Phân hệ phần mềm
              _buildSectionLabel('1. Phân hệ gặp sự cố *'),
              DropdownButtonFormField<String>(
                value: _selectedModule,
                decoration: InputDecoration(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  border: OutlineInputBorder(
                    borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                  ),
                ),
                items: _modules.map((m) {
                  return DropdownMenuItem(
                    value: m['id'],
                    child: Text(m['name']!, style: const TextStyle(fontSize: 14)),
                  );
                }).toList(),
                onChanged: (val) {
                  if (val != null) setState(() => _selectedModule = val);
                },
              ),
              const SizedBox(height: 20),

              // 2. Lựa chọn phòng ban
              _buildSectionLabel('2. Lựa chọn phòng ban tiếp nhận *'),
              Column(
                children: _departments.map((dept) {
                  final isSelected = _selectedDepartment == dept['id'];
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 8),
                    child: InkWell(
                      onTap: () => setState(() => _selectedDepartment = dept['id']!),
                      borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                      child: Container(
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: isSelected
                              ? primaryColor.withValues(alpha: 0.06)
                              : theme.colorScheme.surface,
                          borderRadius: BorderRadius.circular(AppTheme.radiusMedium),
                          border: Border.all(
                            color: isSelected
                                ? primaryColor
                                : theme.colorScheme.outline.withValues(alpha: 0.25),
                            width: isSelected ? 1.5 : 1,
                          ),
                        ),
                        child: Row(
                          children: [
                            Radio<String>(
                              value: dept['id']!,
                              groupValue: _selectedDepartment,
                              activeColor: primaryColor,
                              onChanged: (val) {
                                if (val != null) {
                                  setState(() => _selectedDepartment = val);
                                }
                              },
                            ),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    dept['title']!,
                                    style: TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 13,
                                      color: isSelected
                                          ? primaryColor
                                          : theme.colorScheme.onSurface,
                                    ),
                                  ),
                                  const SizedBox(height: 2),
                                  Text(
                                    dept['desc']!,
                                    style: TextStyle(
                                      fontSize: 11,
                                      color: theme.colorScheme.onSurface
                                          .withValues(alpha: 0.65),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  );
                }).toList(),
              ),
              const SizedBox(height: 16),

              // 3. Mức độ ưu tiên
              _buildSectionLabel('3. Mức độ ưu tiên *'),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: Row(
                  children: _priorities.map((p) {
                    final isSelected = _selectedPriority == p['value'];
                    return Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Text(
                              p['label']!,
                              style: const TextStyle(
                                fontWeight: FontWeight.bold,
                                fontSize: 13,
                              ),
                            ),
                            Text(p['desc']!, style: const TextStyle(fontSize: 10)),
                          ],
                        ),
                        selected: isSelected,
                        selectedColor: primaryColor.withValues(alpha: 0.15),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(12),
                          side: BorderSide(
                            color: isSelected
                                ? primaryColor
                                : theme.colorScheme.outline.withValues(alpha: 0.3),
                          ),
                        ),
                        onSelected: (val) {
                          if (val) setState(() => _selectedPriority = p['value']!);
                        },
                      ),
                    );
                  }).toList(),
                ),
              ),
              const SizedBox(height: 20),

              // 4. Tiêu đề yêu cầu
              _buildSectionLabel('4. Tiêu đề yêu cầu *'),
              TextFormField(
                controller: _subjectController,
                decoration: const InputDecoration(
                  hintText: 'Nhập tiêu đề ngắn gọn (vd: Lỗi ký số hợp đồng)',
                ),
                validator: (val) {
                  if (val == null || val.trim().isEmpty) {
                    return 'Vui lòng nhập tiêu đề yêu cầu';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 20),

              // 5. Nội dung mô tả chi tiết
              _buildSectionLabel('5. Nội dung câu hỏi / mô tả sự cố *'),
              TextFormField(
                controller: _descriptionController,
                maxLines: 5,
                decoration: const InputDecoration(
                  hintText:
                      'Mô tả chi tiết các bước dẫn đến lỗi, mã lỗi hoặc yêu cầu cụ thể...',
                  alignLabelWithHint: true,
                ),
                validator: (val) {
                  if (val == null || val.trim().isEmpty) {
                    return 'Vui lòng nhập nội dung chi tiết';
                  }
                  return null;
                },
              ),
              const SizedBox(height: 20),

              // 6. Đính kèm hình ảnh/tệp tin minh họa
              _buildSectionLabel(
                '6. Hình ảnh minh họa / Tệp tin (*.png, *.jpg, *.pdf tối đa 10MB)',
              ),
              if (_attachedFiles.isNotEmpty) ...[
                Column(
                  children: _attachedFiles.asMap().entries.map((entry) {
                    final idx = entry.key;
                    final item = entry.value;
                    final hasLocalPreview = item.localPath != null &&
                        item.isImage &&
                        File(item.localPath!).existsSync();

                    return Container(
                      margin: const EdgeInsets.only(bottom: 8),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 8,
                      ),
                      decoration: BoxDecoration(
                        color: theme.colorScheme.surfaceContainerHighest
                            .withValues(alpha: 0.45),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: theme.colorScheme.outline.withValues(alpha: 0.25),
                        ),
                      ),
                      child: Row(
                        children: [
                          if (hasLocalPreview)
                            ClipRRect(
                              borderRadius: BorderRadius.circular(8),
                              child: Image.file(
                                File(item.localPath!),
                                width: 42,
                                height: 42,
                                fit: BoxFit.cover,
                              ),
                            )
                          else
                            Container(
                              width: 42,
                              height: 42,
                              decoration: BoxDecoration(
                                color: primaryColor.withValues(alpha: 0.12),
                                borderRadius: BorderRadius.circular(8),
                              ),
                              child: Icon(
                                item.isImage
                                    ? Icons.image_rounded
                                    : Icons.picture_as_pdf_rounded,
                                color: primaryColor,
                                size: 22,
                              ),
                            ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  item.name,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.w600,
                                  ),
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                ),
                                Text(
                                  item.isImage ? 'Hình ảnh đính kèm' : 'Tài liệu đính kèm',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: theme.colorScheme.onSurface
                                        .withValues(alpha: 0.6),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          IconButton(
                            onPressed: () => _removeAttachment(idx),
                            icon: const Icon(
                              Icons.delete_outline_rounded,
                              color: Colors.redAccent,
                              size: 20,
                            ),
                            tooltip: 'Xóa tệp',
                          ),
                        ],
                      ),
                    );
                  }).toList(),
                ),
                const SizedBox(height: 6),
              ],

              InkWell(
                onTap: _openAttachmentPickerSheet,
                borderRadius: BorderRadius.circular(12),
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(vertical: 14, horizontal: 16),
                  decoration: BoxDecoration(
                    color: primaryColor.withValues(alpha: 0.04),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(
                      color: primaryColor.withValues(alpha: 0.35),
                    ),
                  ),
                  child: Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(
                        Icons.add_photo_alternate_rounded,
                        color: primaryColor,
                        size: 22,
                      ),
                      const SizedBox(width: 8),
                      Text(
                        'Chọn ảnh từ Thư viện / Thư mục tệp (${_attachedFiles.length}/3)',
                        style: TextStyle(
                          fontWeight: FontWeight.w600,
                          fontSize: 13,
                          color: primaryColor,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
              const SizedBox(height: 32),

              // Nút Gửi yêu cầu
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: _isSubmitting ? null : _handleSubmit,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: primaryColor,
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(vertical: 16),
                  ),
                  child: _isSubmitting
                      ? const SizedBox(
                          height: 20,
                          width: 20,
                          child: CircularProgressIndicator(
                            color: Colors.white,
                            strokeWidth: 2,
                          ),
                        )
                      : const Text(
                          'GỬI YÊU CẦU HỖ TRỢ',
                          style: TextStyle(
                            fontWeight: FontWeight.bold,
                            fontSize: 15,
                            letterSpacing: 0.5,
                          ),
                        ),
                ),
              ),
              const SizedBox(height: 24),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildSectionLabel(String label) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Text(
        label,
        style: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.bold,
          letterSpacing: 0.2,
        ),
      ),
    );
  }
}
