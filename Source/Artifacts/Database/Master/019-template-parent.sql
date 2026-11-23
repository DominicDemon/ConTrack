-- 019: MAU VAN BAN CON GAN VOI MAU HOP DONG. Chay sau MasterDB-ConTrack.sql; chay lai nhieu lan khong sao.
-- Mot mau phu luc / bien ban ban giao / bien lai / de nghi thanh toan (loai 2-5) co the gan vao mot mau hop dong
-- (loai 1). Mo hop dong tao tu mau cha thi thay cac mau con o muc "Van ban di kem" de tao van ban con
-- (CONTRACT_DOCUMENT_RELATION). Mau cu khong co mau cha: cot de NULL.

SET QUOTED_IDENTIFIER ON; -- filtered index (sqlcmd mac dinh OFF)
GO

IF COL_LENGTH('CONTRACT_TEMPLATE', 'parent_template_id') IS NULL
BEGIN
    ALTER TABLE CONTRACT_TEMPLATE ADD parent_template_id BIGINT NULL
        CONSTRAINT FK_CONTRACT_TEMPLATE_PARENT REFERENCES CONTRACT_TEMPLATE (contract_template_id);
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContractTemplate_Parent' AND object_id = OBJECT_ID('CONTRACT_TEMPLATE'))
    CREATE INDEX IX_ContractTemplate_Parent ON CONTRACT_TEMPLATE (parent_template_id) WHERE parent_template_id IS NOT NULL;
GO

-- Tim nhanh van ban con cua mot hop dong.
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ContractDocumentRelation_Parent' AND object_id = OBJECT_ID('CONTRACT_DOCUMENT_RELATION'))
    CREATE INDEX IX_ContractDocumentRelation_Parent ON CONTRACT_DOCUMENT_RELATION (parent_document_id);
GO
