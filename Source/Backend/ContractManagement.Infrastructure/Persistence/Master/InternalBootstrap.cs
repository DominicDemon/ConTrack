using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalBootstrap(MasterDbContext db, InternalTemplateStorage storage)
{
    public async Task Run(string salePassword, string adminPassword, CancellationToken ct = default)
    {
        foreach (var password in new[] { salePassword, adminPassword })
            if (password.Length < 12 || !password.Any(char.IsDigit) || !password.Any(char.IsUpper) || !password.Any(char.IsLower))
                throw new InvalidOperationException("InternalBootstrap passwords need 12+ characters with uppercase, lowercase and digits.");
        var assembly = typeof(InternalBootstrap).Assembly;
        using (var reader = new StreamReader(assembly.GetManifestResourceStream("InternalDraftUpgrade.sql")!))
            await db.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(ct), ct);
        string reviewSql;
        using (var reader = new StreamReader(assembly.GetManifestResourceStream("ContractReviewUpgrade.sql")!))
            reviewSql = await reader.ReadToEndAsync(ct);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            foreach (var batch in Regex.Split(reviewSql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(b => !string.IsNullOrWhiteSpace(b)))
                await db.Database.ExecuteSqlRawAsync(batch, ct);
            await tx.CommitAsync(ct);
        });
        // An explicit bootstrap command, never a background worker or normal startup side effect.
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var group = await db.SystemPermissionGroups.SingleOrDefaultAsync(g => g.SystemPermissionGroupCode == "INTERNAL_DRAFTS", ct);
            if (group is null) { group = new() { SystemPermissionGroupCode = "INTERNAL_DRAFTS", SystemPermissionGroupName = "Hợp đồng nháp nội bộ", CreatedAt = DateTime.UtcNow }; db.SystemPermissionGroups.Add(group); await db.SaveChangesAsync(ct); }
            foreach (var (code, label) in new[] { ("draft.write", "Tạo và sửa nháp của mình"), ("draft.manage.all", "Quản lý nháp toàn công ty") })
                if (!await db.SystemPermissions.AnyAsync(p => p.SystemPermissionCode == code, ct)) db.SystemPermissions.Add(new() {
                    SystemPermissionGroupId = group.SystemPermissionGroupId, SystemPermissionCode = code, SystemPermissionName = label, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            foreach (var (roleCode, label, username, password) in new[] { ("INTERNAL_SALE", "Nhân viên kinh doanh", "sale.local", salePassword), ("INTERNAL_OFFICE", "Hành chính văn phòng", "office.local", adminPassword) })
            {
                var role = await db.SystemRoles.SingleOrDefaultAsync(r => r.SystemRoleCode == roleCode, ct);
                if (role is null) { role = new() { SystemRoleCode = roleCode, SystemRoleName = label, SystemRoleScope = "INTERNAL", SystemRoleStatus = "ACTIVE", CreatedAt = DateTime.UtcNow }; db.SystemRoles.Add(role); await db.SaveChangesAsync(ct); }
                var permissions = await db.SystemPermissions.Where(p => p.SystemPermissionCode == "draft.write" || (roleCode == "INTERNAL_OFFICE" && p.SystemPermissionCode == "draft.manage.all")).ToListAsync(ct);
                foreach (var permission in permissions) if (!await db.SystemRolePermissions.AnyAsync(r => r.SystemRoleId == role.SystemRoleId && r.SystemPermissionId == permission.SystemPermissionId, ct))
                    db.SystemRolePermissions.Add(new() { SystemRoleId = role.SystemRoleId, SystemPermissionId = permission.SystemPermissionId, CreatedAt = DateTime.UtcNow });
                var employee = await db.Employees.SingleOrDefaultAsync(e => e.EmployeeUsername == username, ct);
                if (employee is null)
                {
                    employee = new() { EmployeeCode = roleCode, EmployeeUsername = username, EmployeeDisplayName = label + " (local)", EmployeeStatus = "ACTIVE", CreatedAt = DateTime.UtcNow,
                        EmployeeEmail = username + "@example.invalid", EmployeePhone = username, EmployeePersonalIdentifier = "LOCAL-" + username };
                    employee.EmployeePasswordHash = new PasswordHasher<Employee>().HashPassword(employee, password);
                    db.Employees.Add(employee); await db.SaveChangesAsync(ct);
                }
                if (!await db.EmployeeRoles.AnyAsync(r => r.EmployeeId == employee.EmployeeId && r.SystemRoleId == role.SystemRoleId, ct)) db.EmployeeRoles.Add(new() {
                    EmployeeId = employee.EmployeeId, SystemRoleId = role.SystemRoleId, CreatedAt = DateTime.UtcNow, SystemMemberRoleAssignedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct);
            }
            if (!await db.ApprovalWorkflows.AnyAsync(w => w.ApprovalWorkflowCode == ContractReviewRepository.InternalCheckWorkflow, ct))
            {
                var workflow = new ApprovalWorkflow { ApprovalWorkflowCode = ContractReviewRepository.InternalCheckWorkflow, ApprovalWorkflowName = "Kiểm tra nội bộ hợp đồng",
                    ApprovalWorkflowDescription = "Hành chính kiểm tra sau khi khách đồng ý bản nháp.", ApprovalWorkflowObjecttype = "CONTRACT_DOCUMENT",
                    ApprovalWorkflowIsactive = true, ApprovalWorkflowCreatedat = DateTime.UtcNow };
                db.ApprovalWorkflows.Add(workflow); await db.SaveChangesAsync(ct);
                var office = await db.SystemRoles.SingleAsync(r => r.SystemRoleCode == "INTERNAL_OFFICE", ct);
                db.ApprovalWorkflowSteps.Add(new() { ApprovalWorkflowId = workflow.ApprovalWorkflowId, ApprovalWorkflowStepOrder = 1,
                    ApprovalWorkflowStepName = "Hành chính kiểm tra", SystemRoleId = office.SystemRoleId, ApprovalWorkflowStepIsrequired = true });
                await db.SaveChangesAsync(ct);
            }
            const string templateCode = "INTERNAL_DEMO_V1";
            var template = await db.ContractTemplates.SingleOrDefaultAsync(t => t.ContractTemplateCode == templateCode, ct);
            if (template is null) { template = new() { ContractTemplateCode = templateCode, ContractTemplateName = "Hợp đồng cung cấp sản phẩm/dịch vụ — mẫu minh họa",
                ContractTemplateDescription = "Khung từ Mẫu Contract Placeholder tiếng Việt; chưa được công ty phê duyệt.", ContractTemplateDocumentType = 1, ContractTemplateLanguageMode = 1, IsActive = true, CreatedDate = DateTime.UtcNow };
                db.ContractTemplates.Add(template); await db.SaveChangesAsync(ct); }
            var fields = new List<DraftField> {
                new("CUSTOMER_COMPANY", "Tên khách hàng / doanh nghiệp", true), new("CUSTOMER_CONTACT_NAME", "Người đại diện khách hàng", true),
                new("CUSTOMER_CONTACT_TITLE", "Chức vụ người đại diện khách"), new("CUSTOMER_CONTACT_PHONE", "SĐT khách hàng", true),
                new("CUSTOMER_EMAIL", "Email khách hàng"), new("CUSTOMER_ADDRESS", "Địa chỉ khách hàng", true), new("CUSTOMER_TAX_CODE", "Mã số thuế khách hàng"),
                new("CUSTOMER_BANK_ACCOUNT_NUMBER", "Tài khoản ngân hàng khách hàng"), new("CUSTOMER_BANK_NAME", "Ngân hàng khách hàng"),
                new("PROVIDER_LEGAL_NAME", "Tên pháp lý công ty mình", true), new("PROVIDER_REPRESENTATIVE_NAME", "Người đại diện công ty", true),
                new("PROVIDER_REPRESENTATIVE_TITLE", "Chức vụ người đại diện công ty"), new("PROVIDER_ADDRESS", "Địa chỉ công ty", true),
                new("PROVIDER_PHONE", "SĐT công ty"), new("PROVIDER_TAX_CODE", "Mã số thuế công ty", true),
                new("PROVIDER_BANK_ACCOUNT_NUMBER", "Tài khoản ngân hàng công ty"), new("PROVIDER_BANK_NAME", "Ngân hàng công ty"),
                new("CONTRACT_LEGAL_BASES", "Căn cứ hợp đồng") };
            using var source = assembly.GetManifestResourceStream("InternalDraftTemplate.docx")!;
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, ct));
            var definition = new TemplateDefinition(template.ContractTemplateId, template.ContractTemplateName, "demo-v1-" + hash[..12], true, fields,
                [new("SCOPE", "Phạm vi cung cấp", ""), new("PAYMENT", "Giá trị và điều kiện thanh toán", ""), new("DELIVERY", "Bàn giao và nghiệm thu", "")]);
            if (!await db.FileStorages.AnyAsync(f => f.FileStorageObjectid == template.ContractTemplateId && f.FileStorageObjecttype == "INTERNAL_TEMPLATE", ct))
            {
                Directory.CreateDirectory(storage.Root);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(definition, InternalRepository.Json);
                var key = $"template-{template.ContractTemplateId}-{definition.Revision}.json";
                await File.WriteAllBytesAsync(Path.Combine(storage.Root, key), bytes, ct);
                db.FileStorages.Add(new() { FileStorageObjecttype = "INTERNAL_TEMPLATE", FileStorageObjectid = template.ContractTemplateId,
                    FileStorageFiletype = "TEMPLATE_JSON", FileStorageFilename = key, FileStorageFilepath = key, FileStorageStoragekey = key,
                    FileStorageContenttype = "application/json", FileStorageFilesize = bytes.Length, FileStorageSha256 = Convert.ToHexString(SHA256.HashData(bytes)), FileStorageUploadeddate = DateTime.UtcNow });
            }
            foreach (var field in fields) if (!await db.ContractTemplateFields.AnyAsync(f => f.ContractTemplateId == template.ContractTemplateId && f.ContractTemplateFieldKey == field.Key, ct))
                db.ContractTemplateFields.Add(new() { ContractTemplateId = template.ContractTemplateId, ContractTemplateFieldKey = field.Key,
                    ContractTemplateFieldLabel = field.Label, ContractTemplateFieldDataType = "TEXT", IsRequired = field.Required });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
