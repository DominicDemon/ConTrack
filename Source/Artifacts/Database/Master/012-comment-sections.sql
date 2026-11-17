-- [012] Góp ý / yêu cầu sửa của khách theo mục hợp đồng, không chỉ theo điều khoản.
-- Một góp ý gắn với một điều khoản (contract_negotiation_comment_termid, như trước) hoặc với một mục khác của hợp đồng:
--   PARTIES = Thông tin các bên, DATES = Thời hạn hợp đồng, ITEMS = Hạng mục và giá trị, PAYMENT = Lịch thanh toán.
-- Không gắn gì là góp ý chung. Chạy sau 001–011 (sqlcmd -d <tên database> -I). Chạy lại không đổi gì.
IF COL_LENGTH('dbo.CONTRACT_NEGOTIATION_COMMENT', 'contract_negotiation_comment_section') IS NULL
    ALTER TABLE dbo.CONTRACT_NEGOTIATION_COMMENT ADD contract_negotiation_comment_section VARCHAR(20) NULL;
GO
