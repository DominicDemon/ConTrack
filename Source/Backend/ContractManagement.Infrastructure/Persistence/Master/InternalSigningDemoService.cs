using System.Security.Cryptography;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalSigningDemoService(
    MasterDbContext db,
    IInternalRepository repository,
    InternalDraftService drafts,
    IHostEnvironment environment)
{
    public async Task<SigningDemoDraftResult> Create(InternalUser actor, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) throw new InternalAppException(404, "Không tìm thấy chức năng.");
        if (!actor.CanManageAll) throw new InternalAppException(403, "Chỉ SystemAdmin được tạo hợp đồng demo để thử luồng ký.");

        var template = (await repository.Templates(ct)).FirstOrDefault(t => t.IsDemo && !t.IsImported)
            ?? throw new InternalAppException(409, "Chưa có mẫu demo. Hãy chạy --seed-internal để khởi tạo mẫu hợp đồng.");
        var now = DateTime.UtcNow;
        string phone;
        do
        {
            phone = "09" + RandomNumberGenerator.GetInt32(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        } while (await db.Customers.AnyAsync(c => c.CustomerPersonalPhone == phone, ct));

        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
        var customerName = $"Khách hàng demo ký hợp đồng {now:yyyyMMddHHmmss}-{suffix}";
        var password = "DemoA1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();
        var taxCode = RandomNumberGenerator.GetInt32(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture) + "00";
        var address = "12 Đường Demo, Phường Bến Nghé, TP. Hồ Chí Minh";
        var customer = await repository.CreateCustomer(new CustomerInput(customerName, phone,
            $"{suffix.ToLowerInvariant()}@example.invalid", "Công ty Demo ConTrack", taxCode, address), actor.Id, ct);

        var customerEntity = await db.Customers.SingleAsync(c => c.CustomerId == customer.Id, ct);
        customerEntity.CustomerPersonalPhoneVerifiedAt = now;
        customerEntity.CustomerPasswordHash = new PasswordHasher<Customer>().HashPassword(customerEntity, password);
        db.AuditLogs.Add(new AuditLog
        {
            ActionType = "ENABLE_DEVELOPMENT_SIGNING_DEMO_ACCESS",
            EntityName = "CUSTOMER",
            EntityId = customer.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ActorType = "EMPLOYEE",
            ActorId = actor.Id,
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);

        var fields = new Dictionary<string, string>(customer.Fields, StringComparer.Ordinal)
        {
            ["CUSTOMER_COMPANY"] = "Công ty Demo ConTrack",
            ["CUSTOMER_CONTACT_NAME"] = customerName,
            ["CUSTOMER_CONTACT_TITLE"] = "Giám đốc",
            ["CUSTOMER_CONTACT_PHONE"] = phone,
            ["CUSTOMER_EMAIL"] = $"{suffix.ToLowerInvariant()}@example.invalid",
            ["CUSTOMER_ADDRESS"] = address,
            ["CUSTOMER_TAX_CODE"] = taxCode,
            ["PROVIDER_LEGAL_NAME"] = "Công ty ConTrack Demo",
            ["PROVIDER_REPRESENTATIVE_NAME"] = "Nguyễn Minh An",
            ["PROVIDER_REPRESENTATIVE_TITLE"] = "Giám đốc",
            ["PROVIDER_ADDRESS"] = "Tầng 5, 123 Nguyễn Huệ, TP. Hồ Chí Minh",
            ["PROVIDER_PHONE"] = "0900000000",
            ["PROVIDER_TAX_CODE"] = "0312345678",
            ["PROVIDER_BANK_ACCOUNT_NUMBER"] = "0123456789",
            ["PROVIDER_BANK_NAME"] = "Ngân hàng Demo",
            ["CONTRACT_LEGAL_BASES"] = "Các bên tự nguyện giao kết hợp đồng để thử nghiệm quy trình ký điện tử trên môi trường Development."
        };
        var terms = template.Terms.Select(term => term with
        {
            Content = term.Key switch
            {
                "SCOPE" => "Bên cung cấp triển khai gói dịch vụ phần mềm demo và hướng dẫn khách hàng sử dụng.",
                "PAYMENT" => "Giá trị demo là 10.000.000 VND, thanh toán trong vòng 10 ngày kể từ ngày ký.",
                "DELIVERY" => "Dịch vụ demo được bàn giao trong vòng 05 ngày làm việc sau khi hợp đồng có hiệu lực.",
                _ => string.IsNullOrWhiteSpace(term.Content) ? "Các bên thực hiện theo thỏa thuận và quy định pháp luật hiện hành." : term.Content
            }
        }).ToList();
        var content = new DraftContent
        {
            TemplateId = template.Id,
            CustomerId = customer.Id,
            Name = $"BẢN DEMO - Hợp đồng dịch vụ ConTrack {now:yyyyMMdd-HHmmss}",
            Fields = fields,
            Items = [new DraftItem(null, "Gói dịch vụ phần mềm demo", 1, 10_000_000m, 0, 0)],
            Terms = terms,
            VatPercent = 10,
            EffectiveDate = DateOnly.FromDateTime(now),
            ExpireDate = DateOnly.FromDateTime(now.AddYears(1)),
            TemplateSnapshot = template
        };
        var draft = await drafts.Save(null, new SaveDraftRequest(content, null, null), actor, ct);
        return new SigningDemoDraftResult(draft, phone, password);
    }
}
