-- BAO CAO BAN SCAN TRUNG (CHI DOC, KHONG SUA GI).
-- Tu ban 012 he thong tu choi tai lai file da co; script nay tim cac ban trung DA LUU TRUOC DO.
-- Moi nhom la mot noi dung file (cung SHA-256) duoc luu nhieu lan, kem noi no duoc dung:
--   SCAN CU = hop dong giay scan (luong B), BAN KY = ban ky giay cua hop dong (luong A).
-- Quan tri xem tung nhom roi quyet dinh giu ban nao; nen giu ban dau tien. Khong xoa file truc tiep tren o dia.

SET NOCOUNT ON;

WITH scans AS (
    SELECT f.file_storage_fileid, f.file_storage_sha256, f.file_storage_filename, f.file_storage_uploadeddate,
           f.file_storage_uploadedbyemployeeid, f.file_storage_storagekey
    FROM FILE_STORAGE f
    WHERE f.file_storage_storagekey LIKE 'scanned-contracts/%' OR f.file_storage_storagekey LIKE 'signed-evidence/%'
), duplicated AS (
    SELECT file_storage_sha256 FROM scans GROUP BY file_storage_sha256 HAVING COUNT(*) > 1
)
SELECT
    DENSE_RANK() OVER (ORDER BY s.file_storage_sha256) AS nhom,
    s.file_storage_fileid AS file_id,
    CASE WHEN s.file_storage_storagekey LIKE 'scanned-contracts/%' THEN N'SCAN CU' ELSE N'BAN KY' END AS loai,
    s.file_storage_filename AS ten_tep,
    DATEADD(HOUR, 7, s.file_storage_uploadeddate) AS tai_luc_gio_vn,
    e.employee_display_name AS nguoi_tai,
    j.ocr_job_id AS scan_job_id,
    d.contract_document_code AS so_hop_dong_ban_ky,
    CASE ev.contract_signed_evidence_status WHEN 1 THEN N'Cho kiem tra' WHEN 2 THEN N'Hop le' WHEN 3 THEN N'Bi tu choi' WHEN 4 THEN N'Da thay' END AS trang_thai_ban_ky,
    CASE WHEN ROW_NUMBER() OVER (PARTITION BY s.file_storage_sha256 ORDER BY s.file_storage_fileid) = 1 THEN N'Ban dau tien (nen giu)' ELSE N'Ban trung' END AS goi_y
FROM scans s
JOIN duplicated x ON x.file_storage_sha256 = s.file_storage_sha256
LEFT JOIN EMPLOYEE e ON e.employee_id = s.file_storage_uploadedbyemployeeid
LEFT JOIN OCR_JOB j ON j.file_storage_fileid = s.file_storage_fileid AND j.contract_document_id IS NULL
LEFT JOIN CONTRACT_DOCUMENT_FILES df ON df.file_storage_fileid = s.file_storage_fileid
LEFT JOIN CONTRACT_DOCUMENTS d ON d.contract_document_id = df.contract_document_id
LEFT JOIN CONTRACT_SIGNED_EVIDENCE ev ON ev.contract_document_file_id = df.contract_document_file_id
ORDER BY nhom, s.file_storage_fileid;
