SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT 1 FROM dbo.DOCUMENT_CATEGORY WITH (UPDLOCK, HOLDLOCK)
    WHERE document_category_name = N'Công văn xử phạt hành chính'
)
BEGIN
    INSERT dbo.DOCUMENT_CATEGORY
        (document_category_name, effective_label, effective_required, expire_shown, expire_label,
         expire_required, items_label, items_required, item_prices, amount_required, milestones,
         milestones_required, parent_milestone)
    VALUES (N'Công văn xử phạt hành chính', N'Ngày văn bản', 0, 0, N'', 0, N'Nội dung', 0, 0, 0, 0, 0, 0);
END;
COMMIT;
GO
