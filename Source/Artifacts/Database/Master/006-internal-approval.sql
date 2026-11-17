SET XACT_ABORT ON;
IF COL_LENGTH('dbo.CONTRACT_APPROVAL_REQUEST', 'internal_stage') IS NULL
    ALTER TABLE dbo.CONTRACT_APPROVAL_REQUEST ADD internal_stage VARCHAR(20) NULL;
IF COL_LENGTH('dbo.CONTRACT_APPROVAL_REQUEST', 'internal_content_hash') IS NULL
    ALTER TABLE dbo.CONTRACT_APPROVAL_REQUEST ADD internal_content_hash VARCHAR(64) NULL;
GO
BEGIN TRANSACTION;
DECLARE @now DATETIME2 = SYSUTCDATETIME();
IF NOT EXISTS (SELECT 1 FROM SYSTEM_PERMISSION WHERE system_permission_code = 'contract.check')
    INSERT SYSTEM_PERMISSION(system_permission_group_id, system_permission_code, system_permission_name, created_at)
    SELECT system_permission_group_id, 'contract.check', N'Kiểm tra hợp đồng trước khi trình Sếp', @now
    FROM SYSTEM_PERMISSION_GROUP WHERE system_permission_group_code = 'INTERNAL_PAGES';
INSERT SYSTEM_ROLE_PERMISSION(system_role_id, system_permission_id, created_at)
SELECT r.system_role_id, p.system_permission_id, @now FROM SYSTEM_ROLE r CROSS JOIN SYSTEM_PERMISSION p
WHERE r.system_role_code IN ('INTERNAL_LEGAL', 'INTERNAL_LEGAL_MANAGER')
AND p.system_permission_code IN ('contract.check', 'template.manage')
AND NOT EXISTS (SELECT 1 FROM SYSTEM_ROLE_PERMISSION x WHERE x.system_role_id = r.system_role_id AND x.system_permission_id = p.system_permission_id);
DELETE rp FROM SYSTEM_ROLE_PERMISSION rp JOIN SYSTEM_ROLE r ON r.system_role_id=rp.system_role_id
JOIN SYSTEM_PERMISSION p ON p.system_permission_id=rp.system_permission_id
WHERE p.system_permission_code='template.manage' AND r.system_role_code NOT IN ('INTERNAL_LEGAL','INTERNAL_LEGAL_MANAGER');
IF NOT EXISTS (SELECT 1 FROM APPROVAL_WORKFLOW WHERE approval_workflow_code='CONTRACT_PRE_SEND')
    INSERT APPROVAL_WORKFLOW(approval_workflow_code,approval_workflow_name,approval_workflow_objecttype,approval_workflow_isactive,approval_workflow_createdat)
    VALUES ('CONTRACT_PRE_SEND', N'Hành chính và Sếp duyệt trước khi gửi khách','CONTRACT',1,@now);
DECLARE @workflow BIGINT=(SELECT approval_workflow_id FROM APPROVAL_WORKFLOW WHERE approval_workflow_code='CONTRACT_PRE_SEND');
INSERT APPROVAL_WORKFLOW_STEP(approval_workflow_id,approval_workflow_step_order,approval_workflow_step_name,system_role_id,approval_workflow_step_isrequired)
SELECT @workflow, v.step_order, v.name, r.system_role_id, 1
FROM (VALUES (1,N'Hành chính kiểm tra','INTERNAL_LEGAL'),(2,N'Sếp duyệt','INTERNAL_ADMIN')) v(step_order,name,role_code)
JOIN SYSTEM_ROLE r ON r.system_role_code=v.role_code
WHERE NOT EXISTS (SELECT 1 FROM APPROVAL_WORKFLOW_STEP s WHERE s.approval_workflow_id=@workflow AND s.approval_workflow_step_order=v.step_order);
COMMIT;
