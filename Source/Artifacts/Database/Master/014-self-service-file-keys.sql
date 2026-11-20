-- 014: NHAN VIEN TU DAT KEY MO TEP. Chay sau 007 va 008; chay lai nhieu lan khong sao. Khong doi cau truc bang.
-- Tu ban nay quan tri khong cap key nua: moi nhan vien duoc xem ban scan tu tao, doi, dat lai key cua minh
-- (muc "Key cua toi", xac nhan bang mat khau dang nhap va ma gui qua email). Quyen system.encryption_key.manage
-- con lai de xem ai da co key, mo lai tep cu cho key moi va thu hoi key. Script nay chi sua ten va mo ta quyen.
-- EMPLOYEE_ENCRYPTION_KEY.issued_by_employee_id / issued_at tu nay la nguoi dat va thoi diem dat key.

UPDATE SYSTEM_PERMISSION_GROUP
SET system_permission_group_name = N'Key mở tệp mã hóa',
    system_permission_group_description = N'Nhân viên tự đặt key để mở bản scan đã mã hóa; quản trị theo dõi và thu hồi key'
WHERE system_permission_group_code = 'FILE_ENCRYPTION';

UPDATE SYSTEM_PERMISSION
SET system_permission_name = N'Quản lý key mở tệp',
    system_permission_description = N'Xem ai đã có key, mở lại tệp cũ cho key mới bằng khóa dự phòng, thu hồi key; không đặt hay xem được key của nhân viên'
WHERE system_permission_code = 'system.encryption_key.manage';
