using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

/// <summary>
/// Pipeline kiểm tra tính toàn vẹn dữ liệu (Immutability, Audit, Snapshot) trước khi lưu thay đổi vào Database.
/// </summary>
public partial class DbDtctechContext
{
    private static long _syntheticRowVersionSeed = 10_000;

    public override int SaveChanges()
    {
        return SaveChanges(acceptAllChangesOnSuccess: true);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RevokeCustomerAccessForCancelledContracts();
        PopulateContractAuditSnapshots();
        ValidateLockedVersionChanges();
        AssignSyntheticRowVersionsForInMemory();
        ValidateContractAuditEntries();
        ValidateContractTemplateAuditEntries();
        ValidateContractPlaceholderAuditEntries();
        ValidateAuthorizationAuditEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(
            acceptAllChangesOnSuccess: true,
            cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RevokeCustomerAccessForCancelledContracts();
        await PopulateContractAuditSnapshotsAsync(cancellationToken);
        await ValidateLockedVersionChangesAsync(cancellationToken);
        AssignSyntheticRowVersionsForInMemory();
        ValidateContractAuditEntries();
        ValidateContractTemplateAuditEntries();
        ValidateContractPlaceholderAuditEntries();
        ValidateAuthorizationAuditEntries();
        return await base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    #region 1. Contract Audit Snapshot & Validation

    private void PopulateContractAuditSnapshots() =>
        PopulateContractAuditSnapshotsAsync(CancellationToken.None, useAsync: false)
            .GetAwaiter()
            .GetResult();

    private Task PopulateContractAuditSnapshotsAsync(
        CancellationToken cancellationToken) =>
        PopulateContractAuditSnapshotsAsync(cancellationToken, useAsync: true);

    private async Task PopulateContractAuditSnapshotsAsync(
        CancellationToken cancellationToken,
        bool useAsync)
    {
        var audits = ChangeTracker.Entries<TblContractAudit>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();
        if (audits.Count == 0)
        {
            return;
        }

        var contractIds = audits.Select(audit => audit.ContractId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var contracts = ChangeTracker.Entries<TblContract>()
            .Where(entry => entry.State != EntityState.Deleted
                && contractIds.Contains(entry.Entity.ContractId))
            .Select(entry => entry.Entity)
            .ToDictionary(contract => contract.ContractId);
        var missingContractIds = contractIds.Except(contracts.Keys).ToList();
        if (missingContractIds.Count > 0)
        {
            var storedContracts = await LoadSnapshotRowsAsync(
                TblContracts.AsNoTracking()
                    .Where(contract => missingContractIds.Contains(contract.ContractId)),
                useAsync,
                cancellationToken);
            foreach (var contract in storedContracts)
            {
                contracts[contract.ContractId] = contract;
            }
        }

        var versionIds = audits.Where(audit => audit.VersionId.HasValue)
            .Select(audit => audit.VersionId!.Value)
            .Distinct()
            .ToList();
        var versions = ChangeTracker.Entries<TblContractVersion>()
            .Where(entry => entry.State != EntityState.Deleted
                && versionIds.Contains(entry.Entity.VersionId))
            .Select(entry => entry.Entity)
            .ToDictionary(version => version.VersionId);
        var missingVersionIds = versionIds.Except(versions.Keys).ToList();
        if (missingVersionIds.Count > 0)
        {
            var storedVersions = await LoadSnapshotRowsAsync(
                TblContractVersions.AsNoTracking()
                    .Where(version => missingVersionIds.Contains(version.VersionId)),
                useAsync,
                cancellationToken);
            foreach (var version in storedVersions)
            {
                versions[version.VersionId] = version;
            }
        }

        var employeeIds = audits.Where(audit => audit.ActorEmployeeId.HasValue)
            .Select(audit => audit.ActorEmployeeId!.Value)
            .Distinct()
            .ToList();
        var employees = ChangeTracker.Entries<TblEmployee>()
            .Where(entry => entry.State != EntityState.Deleted
                && employeeIds.Contains(entry.Entity.EmployeeId))
            .Select(entry => entry.Entity)
            .ToDictionary(employee => employee.EmployeeId);
        var missingEmployeeIds = employeeIds.Except(employees.Keys).ToList();
        if (missingEmployeeIds.Count > 0)
        {
            var storedEmployees = await LoadSnapshotRowsAsync(
                TblEmployees.AsNoTracking()
                    .Where(employee => missingEmployeeIds.Contains(employee.EmployeeId)),
                useAsync,
                cancellationToken);
            foreach (var employee in storedEmployees)
            {
                employees[employee.EmployeeId] = employee;
            }
        }

        var sessionIds = audits
            .Where(audit => audit.ActorCustomerAccessSessionId.HasValue)
            .Select(audit => audit.ActorCustomerAccessSessionId!.Value)
            .Distinct()
            .ToList();
        var sessions = ChangeTracker.Entries<TblContractCustomerAccessSession>()
            .Where(entry => entry.State != EntityState.Deleted
                && sessionIds.Contains(entry.Entity.CustomerAccessSessionId))
            .Select(entry => entry.Entity)
            .ToDictionary(session => session.CustomerAccessSessionId);
        var missingSessionIds = sessionIds.Except(sessions.Keys).ToList();
        if (missingSessionIds.Count > 0)
        {
            var storedSessions = await LoadSnapshotRowsAsync(
                TblContractCustomerAccessSessions.AsNoTracking()
                    .Where(session => missingSessionIds.Contains(
                        session.CustomerAccessSessionId)),
                useAsync,
                cancellationToken);
            foreach (var session in storedSessions)
            {
                sessions[session.CustomerAccessSessionId] = session;
            }
        }

        var customerIds = contracts.Values.Select(contract => contract.CustomerId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var customers = ChangeTracker.Entries<TblCustomer>()
            .Where(entry => entry.State != EntityState.Deleted
                && customerIds.Contains(entry.Entity.CustomerId))
            .Select(entry => entry.Entity)
            .ToDictionary(customer => customer.CustomerId);
        var missingCustomerIds = customerIds.Except(customers.Keys).ToList();
        if (missingCustomerIds.Count > 0)
        {
            var storedCustomers = await LoadSnapshotRowsAsync(
                TblCustomers.AsNoTracking()
                    .Where(customer => missingCustomerIds.Contains(customer.CustomerId)),
                useAsync,
                cancellationToken);
            foreach (var customer in storedCustomers)
            {
                customers[customer.CustomerId] = customer;
            }
        }

        var verificationPhoneIds = sessions.Values
            .Select(session => session.VerificationPhoneId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var verificationPhones = ChangeTracker
            .Entries<TblContractCustomerVerificationPhone>()
            .Where(entry => entry.State != EntityState.Deleted
                && verificationPhoneIds.Contains(entry.Entity.VerificationPhoneId))
            .Select(entry => entry.Entity)
            .ToDictionary(phone => phone.VerificationPhoneId);
        var missingPhoneIds = verificationPhoneIds
            .Except(verificationPhones.Keys)
            .ToList();
        if (missingPhoneIds.Count > 0)
        {
            var storedPhones = await LoadSnapshotRowsAsync(
                TblContractCustomerVerificationPhones.AsNoTracking()
                    .Where(phone => missingPhoneIds.Contains(phone.VerificationPhoneId)),
                useAsync,
                cancellationToken);
            foreach (var phone in storedPhones)
            {
                verificationPhones[phone.VerificationPhoneId] = phone;
            }
        }

        foreach (var audit in audits)
        {
            contracts.TryGetValue(audit.ContractId, out var contract);
            if (contract is not null)
            {
                audit.ContractCodeSnapshot ??= NormalizeAuditSnapshot(
                    contract.ContractCode,
                    50);
                audit.ContractNameSnapshot ??= NormalizeAuditSnapshot(
                    contract.ContractName,
                    1000);
            }

            if (audit.VersionId.HasValue
                && versions.TryGetValue(audit.VersionId.Value, out var version))
            {
                audit.VersionNoSnapshot ??= version.VersionNo;
            }

            if (audit.ActorEmployeeId.HasValue
                && employees.TryGetValue(audit.ActorEmployeeId.Value, out var employee))
            {
                audit.ActorDisplayNameSnapshot ??= NormalizeAuditSnapshot(
                    FirstAuditSnapshotValue(
                        employee.EmployeeFullName,
                        employee.EmployeeCode,
                        employee.EmployeeAccount),
                    1000);
            }

            if (!audit.ActorCustomerAccessSessionId.HasValue
                || !sessions.TryGetValue(
                    audit.ActorCustomerAccessSessionId.Value,
                    out var session))
            {
                continue;
            }

            if (contract is not null
                && customers.TryGetValue(contract.CustomerId, out var customer))
            {
                audit.ActorDisplayNameSnapshot ??= NormalizeAuditSnapshot(
                    FirstAuditSnapshotValue(
                        customer.CustomerFullName,
                        customer.CustomerCompany,
                        customer.CustomerRepresentativeName,
                        customer.CustomerCode),
                    1000);
            }

            if (verificationPhones.TryGetValue(
                    session.VerificationPhoneId,
                    out var phone))
            {
                audit.ActorMaskedPhoneSnapshot ??= MaskAuditPhone(
                    phone.PhoneNumberNormalized);
                audit.ActorPhoneSourceSnapshot ??= NormalizeAuditSnapshot(
                    phone.PhoneSource,
                    32);
            }
        }
    }

    private static async Task<List<T>> LoadSnapshotRowsAsync<T>(
        IQueryable<T> query,
        bool useAsync,
        CancellationToken cancellationToken) where T : class =>
        useAsync
            ? await query.ToListAsync(cancellationToken)
            : query.ToList();

    private static string? FirstAuditSnapshotValue(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? NormalizeAuditSnapshot(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static string? MaskAuditPhone(string? normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var visible = Math.Min(4, normalized.Length);
        return new string('*', normalized.Length - visible) + normalized[^visible..];
    }

    private void AssignSyntheticRowVersionsForInMemory()
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(x => x.State == EntityState.Added
                         && x.Metadata.FindProperty("RowVersion") is not null))
        {
            var property = entry.Property("RowVersion");
            if (property.CurrentValue is not byte[] { Length: 8 })
            {
                property.CurrentValue = BitConverter.GetBytes(
                    Interlocked.Increment(ref _syntheticRowVersionSeed));
            }
        }

        foreach (var entry in ChangeTracker.Entries<TblEmployee>()
                     .Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.RowVersion = BitConverter.GetBytes(
                Interlocked.Increment(ref _syntheticRowVersionSeed));
        }
        foreach (var entry in ChangeTracker.Entries<TblContractPlaceholderDefinition>()
                     .Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.RowVersion = BitConverter.GetBytes(Interlocked.Increment(ref _syntheticRowVersionSeed));
        }
    }

    private void RevokeCustomerAccessForCancelledContracts()
    {
        var cancelledContracts = ChangeTracker.Entries<TblContract>()
            .Where(entry => entry.State == EntityState.Modified
                && entry.Entity.Status == 6
                && entry.OriginalValues.GetValue<byte>(nameof(TblContract.Status)) != 6)
            .Select(entry => entry.Entity)
            .ToList();

        if (cancelledContracts.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var contract in cancelledContracts)
        {
            var actorEmployeeId = contract.UpdatedEmployeeId ?? contract.EmployeeId;
            var links = TblContractCustomerAccessLinks
                .Where(link => link.ContractId == contract.ContractId
                    && link.RevokedAt == null)
                .ToList();
            foreach (var link in links)
            {
                link.RevokedAt = now;
                link.RevokedByEmployeeId = actorEmployeeId;
                link.RevocationReason = "Contract cancelled";

                foreach (var challenge in TblContractCustomerOtpChallenges
                             .Where(challenge => challenge.LinkId
                                 == link.CustomerAccessLinkId
                                 && challenge.InvalidatedAt == null)
                             .ToList())
                {
                    challenge.InvalidatedAt = now;
                }

                foreach (var session in TblContractCustomerAccessSessions
                             .Where(session => session.LinkId
                                 == link.CustomerAccessLinkId
                                 && session.RevokedAt == null)
                             .ToList())
                {
                    session.RevokedAt = now;
                    session.RevocationReason = "Contract cancelled";
                }
            }

            contract.CurrentCustomerAccessLinkId = null;
        }
    }

    private void ValidateContractAuditEntries()
    {
        var changedEntries = ChangeTracker
            .Entries<TblContractAudit>()
            .Where(entry =>
                entry.State == EntityState.Modified
                || entry.State == EntityState.Deleted)
            .ToList();

        if (changedEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Contract audit là dữ liệu append-only và không được sửa hoặc xóa.");
        }

        if (ChangeTracker.Entries<TblContractAuditValue>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Contract audit value là dữ liệu append-only và không được sửa hoặc xóa.");
        }

        foreach (var entry in ChangeTracker
                     .Entries<TblContractAudit>()
                     .Where(entry => entry.State == EntityState.Added))
        {
            ValidateNewContractAudit(entry.Entity);
        }

        foreach (var entry in ChangeTracker.Entries<TblContractAuditValue>()
                     .Where(entry => entry.State == EntityState.Added))
        {
            ValidateAuditScalarValue(
                entry.Entity.ValueSide,
                entry.Entity.FieldCode,
                entry.Entity.ValueKind,
                entry.Entity.IsNull,
                entry.Entity.IntegerValue,
                entry.Entity.DecimalValue,
                entry.Entity.StringValue,
                entry.Entity.DateTimeValue,
                entry.Entity.BooleanValue);
        }
    }

    private void ValidateAuthorizationAuditEntries()
    {
        if (ChangeTracker.Entries<TblAuthorizationAudit>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Authorization audit is append-only and cannot be updated or deleted.");
        }
    }

    private void ValidateContractPlaceholderAuditEntries()
    {
        if (ChangeTracker.Entries<TblContractPlaceholderAudit>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Placeholder audit chỉ được thêm mới.");
        }

        foreach (var audit in ChangeTracker
                     .Entries<TblContractPlaceholderAudit>()
                     .Where(entry => entry.State == EntityState.Added)
                     .Select(entry => entry.Entity))
        {
            var isCreate = audit.ActionType == "PlaceholderDefinitionCreated";
            var isAllowedAction = audit.ActionType is
                "PlaceholderDefinitionCreated"
                or "PlaceholderDefinitionUpdated"
                or "PlaceholderDefinitionActivated"
                or "PlaceholderDefinitionDeactivated"
                or "PlaceholderDefinitionDeleted";
            if (audit.ActorEmployeeId <= 0
                || string.IsNullOrWhiteSpace(audit.PlaceholderKey)
                || !isAllowedAction
                || string.IsNullOrWhiteSpace(audit.NewSourceFieldKey)
                || audit.OccurredAt.Kind != DateTimeKind.Utc
                || (isCreate && (audit.PreviousSourceFieldKey is not null
                    || audit.PreviousFormatString is not null
                    || audit.PreviousIsActive.HasValue))
                || (!isCreate
                    && string.IsNullOrWhiteSpace(audit.PreviousSourceFieldKey)))
            {
                throw new InvalidOperationException(
                    "Placeholder audit typed snapshot không hợp lệ.");
            }
        }
    }

    private static void ValidateNewContractAudit(TblContractAudit audit)
    {
        if (audit.TenantId <= 0
            || audit.ContractId <= 0
            || audit.VersionId is <= 0)
        {
            throw new InvalidOperationException(
                "Contract audit phải tham chiếu Tenant, Contract và Version hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(audit.ActorType))
        {
            throw new InvalidOperationException(
                "Contract audit phải có ActorType.");
        }

        var hasSubjectType = audit.SubjectType is not null;
        var hasSubjectId = audit.SubjectId.HasValue;
        if (hasSubjectType != hasSubjectId
            || (hasSubjectType
                && (string.IsNullOrWhiteSpace(audit.SubjectType)
                    || audit.SubjectId is <= 0
                    || audit.SubjectType is not (
                        "Contract"
                        or "ContractVersion"
                        or "ContractAppendix"
                        or "NegotiationComment"
                        or "CustomerAccessLink"
                        or "CustomerOtpChallenge"
                        or "CustomerAccessSession"
                        or "ApprovalRequest"
                        or "SignedEvidence"
                        or "HandoverRecord"
                        or "AcceptanceRecord"
                        or "AcceptanceEvidence"
                        or "Payment"))))
        {
            throw new InvalidOperationException(
                "Contract audit subject phải nhất quán và hợp lệ.");
        }

        var isEmployeeActor = string.Equals(
            audit.ActorType,
            "Employee",
            StringComparison.Ordinal);

        if (isEmployeeActor && audit.ActorEmployeeId is not > 0)
        {
            throw new InvalidOperationException(
                "Employee audit actor phải có ActorEmployeeId hợp lệ.");
        }

        if (!isEmployeeActor && audit.ActorEmployeeId.HasValue)
        {
            throw new InvalidOperationException(
                "Non-employee audit actor không được có ActorEmployeeId.");
        }

        var isCustomerActor = string.Equals(
            audit.ActorType,
            "Customer",
            StringComparison.Ordinal);
        var isSystemActor = string.Equals(
            audit.ActorType,
            "System",
            StringComparison.Ordinal);

        if (!isEmployeeActor && !isCustomerActor && !isSystemActor)
        {
            throw new InvalidOperationException(
                "Contract audit actor type is invalid.");
        }

        if ((isEmployeeActor && audit.ActorCustomerAccessSessionId.HasValue)
            || (isCustomerActor
                && audit.ActorCustomerAccessSessionId is not > 0)
            || (isSystemActor
                && audit.ActorCustomerAccessSessionId.HasValue))
        {
            throw new InvalidOperationException(
                "Contract audit actor is inconsistent.");
        }

        if (audit.PreviousResponsibleEmployeeId is <= 0
            || audit.NewResponsibleEmployeeId is <= 0)
        {
            throw new InvalidOperationException(
                "Responsible employee trong Contract audit phải hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(audit.ActionType)
            || string.IsNullOrWhiteSpace(audit.Result)
            || string.IsNullOrWhiteSpace(audit.CorrelationId))
        {
            throw new InvalidOperationException(
                "Contract audit phải có action, result và correlation.");
        }

        if (audit.FailureCode is not null
            && string.IsNullOrWhiteSpace(audit.FailureCode))
        {
            throw new InvalidOperationException(
                "Contract audit FailureCode không được rỗng.");
        }

        if (audit.OccurredAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException(
                "Contract audit phải sử dụng timestamp UTC.");
        }
    }

    private static void ValidateAuditScalarValue(
        AuditValueSide side,
        short fieldCode,
        AuditScalarValueKind kind,
        bool isNull,
        long? integerValue,
        decimal? decimalValue,
        string? stringValue,
        DateTime? dateTimeValue,
        bool? booleanValue)
    {
        if (!Enum.IsDefined(side) || fieldCode <= 0 || !Enum.IsDefined(kind))
        {
            throw new InvalidOperationException("Audit scalar metadata không hợp lệ.");
        }

        var populated = (integerValue.HasValue ? 1 : 0)
            + (decimalValue.HasValue ? 1 : 0)
            + (stringValue is not null ? 1 : 0)
            + (dateTimeValue.HasValue ? 1 : 0)
            + (booleanValue.HasValue ? 1 : 0);
        if (isNull)
        {
            if (populated != 0)
            {
                throw new InvalidOperationException(
                    "Audit scalar null không được chứa typed value.");
            }
            return;
        }

        var correctKind = kind switch
        {
            AuditScalarValueKind.Integer => integerValue.HasValue,
            AuditScalarValueKind.Decimal => decimalValue.HasValue,
            AuditScalarValueKind.String => stringValue is { Length: <= 500 },
            AuditScalarValueKind.DateTime => dateTimeValue.HasValue,
            AuditScalarValueKind.Boolean => booleanValue.HasValue,
            _ => false
        };
        if (populated != 1 || !correctKind)
        {
            throw new InvalidOperationException("Audit scalar value không hợp lệ.");
        }
    }

    #endregion

    #region 2. Contract Template Audit Validation

    private void ValidateContractTemplateAuditEntries()
    {
        var changedEntries = ChangeTracker
            .Entries<TblContractTemplateAudit>()
            .Where(entry => entry.State == EntityState.Modified
                || entry.State == EntityState.Deleted)
            .ToList();
        if (changedEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Template audit là dữ liệu append-only và không được sửa hoặc xóa.");
        }

        if (ChangeTracker.Entries<TblContractTemplateAuditValue>().Any(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                "Template audit value là dữ liệu append-only và không được sửa hoặc xóa.");
        }

        foreach (var entry in ChangeTracker
                     .Entries<TblContractTemplateAudit>()
                     .Where(entry => entry.State == EntityState.Added))
        {
            ValidateNewContractTemplateAudit(entry.Entity);
        }

        foreach (var entry in ChangeTracker
                     .Entries<TblContractTemplateAuditValue>()
                     .Where(entry => entry.State == EntityState.Added))
        {
            ValidateContractTemplateAuditValue(entry.Entity);
        }
    }

    private static void ValidateNewContractTemplateAudit(
        TblContractTemplateAudit audit)
    {
        if (audit.TenantId <= 0 || audit.TemplateId <= 0
            || audit.TemplateVersionId <= 0 || audit.ActorEmployeeId <= 0
            || string.IsNullOrWhiteSpace(audit.ActionType)
            || string.IsNullOrWhiteSpace(audit.Result)
            || string.IsNullOrWhiteSpace(audit.CorrelationId)
            || audit.OccurredAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException("Template audit không hợp lệ.");
        }
    }

    private static void ValidateContractTemplateAuditValue(
        TblContractTemplateAuditValue value)
    {
        if (!Enum.IsDefined(value.ValueSide)
            || value.FieldCode is < 1 or > 15)
        {
            throw new InvalidOperationException(
                "Template audit value metadata không hợp lệ.");
        }

        var populated = (value.IntegerValue.HasValue ? 1 : 0)
            + (value.LongValue.HasValue ? 1 : 0)
            + (value.StringValue is not null ? 1 : 0);
        if (value.IsNull)
        {
            if (populated != 0)
            {
                throw new InvalidOperationException(
                    "Template audit null không được chứa typed value.");
            }
            return;
        }

        var correctColumn = value.FieldCode switch
        {
            1 or 5 or 6 or 9 or 12 or 13 or 14 or 15 =>
                value.IntegerValue.HasValue,
            3 or 7 or 10 => value.LongValue.HasValue,
            2 or 4 or 8 or 11 => value.StringValue is { Length: <= 32 },
            _ => false
        };
        if (populated != 1 || !correctColumn)
        {
            throw new InvalidOperationException(
                "Template audit typed value không hợp lệ.");
        }
    }

    #endregion

    #region 3. Contract Version & Handover Immutability

    private void ValidateLockedVersionChanges() =>
        ValidateLockedVersionChangesAsync(CancellationToken.None, useAsync: false)
            .GetAwaiter().GetResult();

    private Task ValidateLockedVersionChangesAsync(CancellationToken cancellationToken) =>
        ValidateLockedVersionChangesAsync(cancellationToken, useAsync: true);

    private async Task ValidateLockedVersionChangesAsync(
        CancellationToken cancellationToken,
        bool useAsync)
    {
        await ValidateHandoverChangesAsync(cancellationToken, useAsync);
        if (ChangeTracker.Entries<TblContractVersionLegalSnapshot>().Any(IsModifiedOrDeleted)
            || ChangeTracker.Entries<TblContractVersionPartySnapshot>().Any(IsModifiedOrDeleted)
            || ChangeTracker.Entries<TblContractVersionPaymentMilestoneSnapshot>().Any(IsModifiedOrDeleted))
        {
            throw new InvalidOperationException(
                "Relational contract snapshot là dữ liệu bất biến và không được sửa hoặc xóa.");
        }

        foreach (var entry in ChangeTracker.Entries<TblContractVersion>())
        {
            if (entry.State == EntityState.Modified
                && entry.OriginalValues.GetValue<bool>(nameof(TblContractVersion.IsLocked)))
            {
                throw new InvalidOperationException(
                    "Version đã khóa là dữ liệu bất biến và không được sửa.");
            }

            if (entry.State == EntityState.Modified
                && entry.Entity.IsLocked
                && !entry.OriginalValues.GetValue<bool>(nameof(TblContractVersion.IsLocked)))
            {
                var snapshot = ChangeTracker.Entries<TblContractVersionLegalSnapshot>()
                    .Where(candidate => candidate.State == EntityState.Added
                        && candidate.Entity.VersionId == entry.Entity.VersionId)
                    .Select(candidate => candidate.Entity)
                    .SingleOrDefault();
                if (snapshot is null
                    || snapshot.Parties.Count != 2
                    || snapshot.Parties.Select(x => x.PartyRole).Distinct().Count() != 2
                    || snapshot.Parties.Any(x => x.PartyRole is not (1 or 2))
                    || string.IsNullOrWhiteSpace(entry.Entity.SnapshotHash))
                {
                    throw new InvalidOperationException(
                        "Version chỉ được khóa cùng một relational snapshot đầy đủ hai bên.");
                }
            }
        }

        var changedVersionIds = ChangeTracker.Entries<TblContractItem>()
            .Where(IsMutated).Select(x => x.Entity.VersionId)
            .Concat(ChangeTracker.Entries<TblContractTerm>()
                .Where(IsMutated).Select(x => x.Entity.VersionId))
            .Concat(ChangeTracker.Entries<TblContractLegalBasis>()
                .Where(IsMutated).Select(x => x.Entity.VersionId))
            .Concat(ChangeTracker.Entries<TblContractVersionPlaceholderValue>()
                .Where(IsMutated).Select(x => x.Entity.VersionId))
            .Concat(ChangeTracker.Entries<TblContractAppendix>()
                .Where(IsMutated).Select(x => x.Entity.VersionId))
            .Where(id => id > 0)
            .ToHashSet();

        var changedAppendixIds = ChangeTracker
            .Entries<TblContractAppendixTerm>()
            .Where(IsMutated)
            .Select(entry => entry.Entity.AppendixId)
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (changedAppendixIds.Length > 0)
        {
            var trackedAppendices = ChangeTracker.Entries<TblContractAppendix>()
                .Where(entry => changedAppendixIds.Contains(
                    entry.Entity.AppendixId))
                .ToDictionary(entry => entry.Entity.AppendixId,
                    entry => entry.Entity.VersionId);
            changedVersionIds.UnionWith(trackedAppendices.Values
                .Where(id => id > 0));
            var unresolvedAppendixIds = changedAppendixIds
                .Where(id => !trackedAppendices.ContainsKey(id))
                .ToArray();
            if (unresolvedAppendixIds.Length > 0)
            {
                var query = TblContractAppendices.AsNoTracking()
                    .Where(appendix => unresolvedAppendixIds.Contains(
                        appendix.AppendixId))
                    .Select(appendix => appendix.VersionId);
                var resolvedVersionIds = useAsync
                    ? await query.ToListAsync(cancellationToken)
                    : query.ToList();
                changedVersionIds.UnionWith(resolvedVersionIds);
            }
        }

        var changedVersionIdArray = changedVersionIds.ToArray();
        if (changedVersionIdArray.Length == 0)
        {
            return;
        }

        var trackedLockedIds = ChangeTracker.Entries<TblContractVersion>()
            .Where(entry => changedVersionIdArray.Contains(entry.Entity.VersionId)
                && entry.Entity.IsLocked)
            .Select(entry => entry.Entity.VersionId)
            .ToHashSet();
        var unresolvedIds = changedVersionIdArray.Except(trackedLockedIds).ToArray();
        if (unresolvedIds.Length > 0)
        {
            var query = TblContractVersions.AsNoTracking()
                .Where(version => unresolvedIds.Contains(version.VersionId)
                    && version.IsLocked)
                .Select(version => version.VersionId);
            var storedLockedIds = useAsync
                ? await query.ToListAsync(cancellationToken)
                : query.ToList();
            trackedLockedIds.UnionWith(storedLockedIds);
        }

        if (trackedLockedIds.Count > 0)
        {
            throw new InvalidOperationException(
                "Item, điều khoản, căn cứ pháp lý, placeholder và phụ lục của version đã khóa là dữ liệu bất biến.");
        }
    }

    private async Task ValidateHandoverChangesAsync(CancellationToken ct, bool useAsync)
    {
        if (ChangeTracker.Entries<TblContractHandoverBaseline>().Any(IsModifiedOrDeleted) ||
            ChangeTracker.Entries<TblContractHandoverScopeItem>().Any(IsModifiedOrDeleted))
            throw new InvalidOperationException("Phạm vi bàn giao đã xác nhận là snapshot bất biến.");
        foreach (var entry in ChangeTracker.Entries<TblContractHandoverRecord>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Không xóa biên bản bàn giao, chỉ hủy theo vòng đời.");
            if (entry.State != EntityState.Modified) continue;
            var oldStatus = entry.OriginalValues.GetValue<byte>(nameof(TblContractHandoverRecord.Status));
            if (oldStatus is 2 or 3)
                throw new InvalidOperationException("Biên bản bàn giao đã ký/hủy là bất biến.");
            if (oldStatus == 1)
            {
                if (entry.Entity.Status is not (2 or 3))
                    throw new InvalidOperationException("Bản bàn giao chốt chỉ được chuyển sang đã ký hoặc đã hủy.");
                string[] allowed = [nameof(TblContractHandoverRecord.Status), nameof(TblContractHandoverRecord.SignedAt),
                    nameof(TblContractHandoverRecord.SignedByEmployeeId), nameof(TblContractHandoverRecord.CancelledAt),
                    nameof(TblContractHandoverRecord.CancelledByEmployeeId), nameof(TblContractHandoverRecord.CancellationReason),
                    nameof(TblContractHandoverRecord.UpdatedAt), nameof(TblContractHandoverRecord.UpdatedEmployeeId), nameof(TblContractHandoverRecord.RowVersion)];
                if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                    throw new InvalidOperationException("Không chỉnh nội dung/snapshot của bản bàn giao đã chốt.");
            }
        }
        var ids = ChangeTracker.Entries().Where(entry => (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                && (entry.Entity is TblContractHandoverLine or TblContractHandoverParty or TblContractHandoverReference or TblContractHandoverSection or TblContractHandoverPlaceholderValue))
            .SelectMany(entry => entry.State == EntityState.Added
                ? new[] { entry.CurrentValues.GetValue<int>("HandoverRecordId") }
                : new[] { entry.CurrentValues.GetValue<int>("HandoverRecordId"), entry.OriginalValues.GetValue<int>("HandoverRecordId") })
            .Distinct().ToArray();
        foreach (var id in ids)
        {
            var tracked = ChangeTracker.Entries<TblContractHandoverRecord>().SingleOrDefault(x => x.Entity.HandoverRecordId == id);
            byte? status;
            if (tracked is not null)
                status = tracked.State == EntityState.Added ? (byte)0 : tracked.OriginalValues.GetValue<byte>(nameof(TblContractHandoverRecord.Status));
            else
            {
                var query = TblContractHandoverRecords.AsNoTracking().Where(x => x.HandoverRecordId == id).Select(x => (byte?)x.Status);
                status = useAsync ? await query.SingleOrDefaultAsync(ct) : query.SingleOrDefault();
            }
            if (status is not 0) throw new InvalidOperationException("Nội dung bản bàn giao đã chốt/ký/hủy không được sửa hoặc thêm.");
        }
        foreach (var id in ChangeTracker.Entries<TblContractAcceptanceScopeAllocation>().Where(IsMutated)
            .SelectMany(x => x.State == EntityState.Added
                ? new[] { x.Entity.AcceptanceRecordId }
                : new[] { x.Entity.AcceptanceRecordId, x.OriginalValues.GetValue<int>(nameof(TblContractAcceptanceScopeAllocation.AcceptanceRecordId)) }).Distinct())
        {
            var tracked = ChangeTracker.Entries<TblContractAcceptanceRecord>().SingleOrDefault(x => x.Entity.AcceptanceRecordId == id);
            byte? status;
            if (tracked is not null)
                status = tracked.State == EntityState.Added ? (byte)0 : tracked.OriginalValues.GetValue<byte>(nameof(TblContractAcceptanceRecord.Status));
            else
            {
                var query = TblContractAcceptanceRecords.AsNoTracking().Where(x => x.AcceptanceRecordId == id).Select(x => (byte?)x.Status);
                status = useAsync ? await query.SingleOrDefaultAsync(ct) : query.SingleOrDefault();
            }
            if (status is not 0) throw new InvalidOperationException("Phạm vi nghiệm thu đã chốt/ký/hủy là bất biến.");
        }
        if (ChangeTracker.Entries<TblContractHandoverEvidence>().Any(IsModifiedOrDeleted))
            throw new InvalidOperationException("Bản ký bàn giao không được sửa/xóa.");
    }

    private static bool IsModifiedOrDeleted<T>(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> entry)
        where T : class => entry.State is EntityState.Modified or EntityState.Deleted;

    private static bool IsMutated<T>(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<T> entry)
        where T : class => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted;

    #endregion
}

