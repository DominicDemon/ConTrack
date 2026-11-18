-- 012: SIET CHAT LUONG SCAN. Chay sau 006-009; chay lai nhieu lan khong sao.
-- 1) Khong luu toan van OCR: file scan da ma hoa, nhung OCR_JOB.ocr_text giu nguyen van hop dong dang chu thuong,
--    ai doc duoc database la doc duoc hop dong ma khong can key. Tim kiem chi dung CONTRACT_SCAN_KEYWORD nen xoa an toan.
-- 2) Them loai tu khoa 5 = Email (dia chi email in tren hop dong, chu thuong).

UPDATE OCR_JOB SET ocr_text = NULL WHERE ocr_text IS NOT NULL;
GO

IF EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_ContractScanKeyword_Type'
           AND definition NOT LIKE '%(5)%')
BEGIN
    ALTER TABLE CONTRACT_SCAN_KEYWORD DROP CONSTRAINT CK_ContractScanKeyword_Type;
    ALTER TABLE CONTRACT_SCAN_KEYWORD ADD CONSTRAINT CK_ContractScanKeyword_Type
        CHECK (contract_scan_keyword_type BETWEEN 1 AND 5); -- 1 So hop dong, 2 Ma so thue, 3 So dien thoai, 4 Ten khach hang, 5 Email
END
GO
