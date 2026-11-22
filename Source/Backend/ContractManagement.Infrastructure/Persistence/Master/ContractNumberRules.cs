using ContractManagement.Application.Features.Internal;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class NumberRule
{
    public string CompanyCode { get; set; } = "";
    public string TypeCode { get; set; } = "";
    public bool IncludeDay { get; set; }
}

public sealed class ContractNumberRules(MasterDbContext db)
{
    public async Task CheckOwner(long owner, long? customerId, CancellationToken ct)
    {
        if (owner < 0 || owner != 0 && !await db.CustomerBusinesses.AnyAsync(b => b.CustomerId == customerId && b.CustomerBusinessId == owner, ct))
            throw new InternalAppException(400, "Pháp nhân không thuộc khách hàng đang chọn.");
    }

    public Task<NumberRule?> Read(long owner, CancellationToken ct) =>
        db.Database.SqlQuery<NumberRule>($"SELECT CompanyCode,TypeCode,IncludeDay FROM dbo.CONTRACT_NUMBER_RULE WHERE owner_id={owner}").SingleOrDefaultAsync(ct);

    public async Task<NumberRule> Save(long owner, long? customerId, NumberRule input, InternalUser user, bool replace, CancellationToken ct)
    {
        if (!user.CanCheck && (!user.CanWrite || owner == 0 || replace))
            throw new InternalAppException(403, "Bạn không có quyền thay đổi quy tắc này.");
        var company = input.CompanyCode?.Trim().ToUpperInvariant() ?? "";
        var type = input.TypeCode?.Trim().ToUpperInvariant() ?? "";
        if (!Regex.IsMatch(company, @"\A[A-Z0-9]{1,16}\z") || !Regex.IsMatch(type, @"\A[A-Z0-9]{1,10}\z"))
            throw new InternalAppException(400, "Dùng chữ A–Z hoặc chữ số. Mã công ty tối đa 16 ký tự, mã loại tối đa 10 ký tự.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await CheckOwner(owner, customerId, ct);
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource='contract-number-rule-write', @LockMode='Exclusive', @LockOwner='Transaction'; IF @r<0 THROW 50000,'Cannot lock numbering rules',1;", ct);
            var existing = await Read(owner, ct);
            if (existing != null && !replace)
                throw new InternalAppException(409, "Pháp nhân đã có quy tắc. Vui lòng kiểm tra quy tắc đã lưu.");
            if (await db.Database.SqlQuery<long>($"SELECT owner_id AS Value FROM dbo.CONTRACT_NUMBER_RULE WHERE CompanyCode={company} AND owner_id<>{owner}").AnyAsync(ct))
                throw new InternalAppException(400, "Mã công ty đã được sử dụng.");
            if (existing == null)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.CONTRACT_NUMBER_RULE(owner_id,CompanyCode,TypeCode,IncludeDay) VALUES({owner},{company},{type},{input.IncludeDay})", ct);
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.CONTRACT_NUMBER_RULE SET CompanyCode={company},TypeCode={type},IncludeDay={input.IncludeDay} WHERE owner_id={owner}", ct);
            await tx.CommitAsync(ct);
            return new NumberRule { CompanyCode=company, TypeCode=type, IncludeDay=input.IncludeDay };
        });
    }
}
