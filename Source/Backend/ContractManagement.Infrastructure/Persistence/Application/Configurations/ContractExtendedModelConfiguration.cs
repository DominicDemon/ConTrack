using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

internal static class ContractExtendedModelConfiguration
{
    internal static void ConfigureAll(ModelBuilder modelBuilder)
    {
        ConfigureCatalogUnits(modelBuilder);
        ConfigureHandover(modelBuilder);
        ConfigurePlaceholders(modelBuilder);
        ConfigureLegalBases(modelBuilder);
        ConfigureContractVersionSnapshots(modelBuilder);
        ConfigureBilingual(modelBuilder);
    }

    internal static void ConfigureCatalogUnits(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblProduct>().Property(x => x.UnitName).HasMaxLength(100).IsUnicode();
        modelBuilder.Entity<TblProduct>().Property(x => x.UnitNameEn).HasMaxLength(100).IsUnicode();
        modelBuilder.Entity<TblService>().Property(x => x.UnitName).HasMaxLength(100).IsUnicode();
        modelBuilder.Entity<TblService>().Property(x => x.UnitNameEn).HasMaxLength(100).IsUnicode();
    }

    internal static void ConfigureHandover(ModelBuilder model)
    {
        model.Entity<TblContractHandoverBaseline>(e =>
        {
            e.ToTable("tbl_ContractHandoverBaseline");
            e.HasKey(x => x.HandoverBaselineId);
            e.HasIndex(x => new { x.ContractId, x.ContractVersionId }).IsUnique();
        });
        model.Entity<TblContractHandoverScopeItem>(e =>
        {
            e.ToTable("tbl_ContractHandoverScopeItem", t =>
            {
                t.HasCheckConstraint("CK_HandoverScope_Mode", "[ScopeMode] IN (1,2)");
                t.HasCheckConstraint("CK_HandoverScope_Quantity", "[SourceQuantity] > 0 AND [RequiredQuantity] > 0 AND (([ScopeMode] = 1 AND [RequiredQuantity] = [SourceQuantity]) OR ([ScopeMode] = 2 AND [RequiredQuantity] = 1))");
            });
            e.HasKey(x => x.HandoverScopeItemId);
            e.HasIndex(x => new { x.HandoverBaselineId, x.SourceContractItemId }).IsUnique();
            e.HasIndex(x => new { x.HandoverBaselineId, x.DisplayOrder }).IsUnique();
            e.Property(x => x.ItemCode).HasMaxLength(100);
            e.Property(x => x.NameVi).HasMaxLength(2000);
            e.Property(x => x.NameEn).HasMaxLength(2000);
            e.Property(x => x.UnitVi).HasMaxLength(100);
            e.Property(x => x.UnitEn).HasMaxLength(100);
            e.Property(x => x.SourceQuantity).HasPrecision(18, 4);
            e.Property(x => x.RequiredQuantity).HasPrecision(18, 4);
        });
        model.Entity<TblContractHandoverRecord>(e =>
        {
            e.ToTable("tbl_ContractHandoverRecord", t =>
            {
                t.HasCheckConstraint("CK_HandoverRecord_Status", "[Status] IN (0,1,2,3)");
                t.HasCheckConstraint("CK_HandoverRecord_Hash", "([SnapshotHash] IS NULL OR LEN([SnapshotHash]) = 64) AND ([Status] NOT IN (1,2) OR [SnapshotHash] IS NOT NULL)");
            });
            e.HasKey(x => x.HandoverRecordId);
            e.HasIndex(x => new { x.ContractId, x.HandoverCode }).IsUnique();
            e.HasIndex(x => new { x.ContractId, x.ContractVersionId }).IsUnique().HasFilter("[Status] IN (0,1)").HasDatabaseName("UX_HandoverRecord_ActiveVersion");
            e.HasIndex(x => new { x.ContractVersionId, x.Status });
            e.Property(x => x.HandoverCode).HasMaxLength(100);
            e.Property(x => x.Location).HasMaxLength(500);
            e.Property(x => x.LocationEn).HasMaxLength(500);
            e.Property(x => x.CancellationReason).HasMaxLength(1000);
            e.Property(x => x.SnapshotHash).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ContractSignDate).HasColumnType("date");
            e.Property(x => x.HandoverDate).HasColumnType("date");
            e.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        });
        model.Entity<TblContractHandoverLine>(e =>
        {
            e.ToTable("tbl_ContractHandoverLine", t => t.HasCheckConstraint("CK_HandoverLine_Quantity", "[Quantity] > 0"));
            e.HasKey(x => x.HandoverLineId);
            e.HasIndex(x => new { x.HandoverRecordId, x.HandoverScopeItemId }).IsUnique();
            e.HasIndex(x => x.HandoverScopeItemId);
            e.HasIndex(x => new { x.HandoverRecordId, x.DisplayOrder }).IsUnique();
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.NotesVi).HasMaxLength(2000);
            e.Property(x => x.NotesEn).HasMaxLength(2000);
        });
        model.Entity<TblContractHandoverParty>(e =>
        {
            e.ToTable("tbl_ContractHandoverParty", t => t.HasCheckConstraint("CK_HandoverParty_Subject", "[SubjectKind] IN (1,2)"));
            e.HasKey(x => x.HandoverPartyId);
            e.HasIndex(x => new { x.HandoverRecordId, x.SubjectKind }).IsUnique();
            e.Property(x => x.LegalName).HasMaxLength(1000);
            e.Property(x => x.LegalNameEn).HasMaxLength(1000);
            e.Property(x => x.Address).HasMaxLength(2000);
            e.Property(x => x.AddressEn).HasMaxLength(2000);
            e.Property(x => x.RepresentativeName).HasMaxLength(300);
            e.Property(x => x.RepresentativeTitle).HasMaxLength(500);
            e.Property(x => x.RepresentativeTitleEn).HasMaxLength(500);
            e.Property(x => x.Phone).HasMaxLength(100);
            e.Property(x => x.Fax).HasMaxLength(100);
            e.Property(x => x.TaxCode).HasMaxLength(50);
        });
        model.Entity<TblContractHandoverReference>(e =>
        {
            e.ToTable("tbl_ContractHandoverReference");
            e.HasKey(x => x.HandoverReferenceId);
            e.HasIndex(x => new { x.HandoverRecordId, x.DisplayOrder }).IsUnique();
        });
        model.Entity<TblContractHandoverSection>(e =>
        {
            e.ToTable("tbl_ContractHandoverSection", t => t.HasCheckConstraint("CK_HandoverSection_Mode", "[BilingualContentMode] IN (1,2)"));
            e.HasKey(x => x.HandoverSectionId);
            e.HasIndex(x => new { x.HandoverRecordId, x.SectionCode }).IsUnique();
            e.HasIndex(x => new { x.HandoverRecordId, x.DisplayOrder }).IsUnique();
            e.Property(x => x.SectionCode).HasMaxLength(100);
            e.Property(x => x.TitleVi).HasMaxLength(1000);
            e.Property(x => x.TitleEn).HasMaxLength(1000);
            e.Property(x => x.ContentVi).HasColumnType("nvarchar(max)");
            e.Property(x => x.ContentEn).HasColumnType("nvarchar(max)");
        });
        model.Entity<TblContractHandoverEvidence>(e =>
        {
            e.ToTable("tbl_ContractHandoverEvidence");
            e.HasKey(x => x.HandoverEvidenceId);
            e.HasIndex(x => x.HandoverRecordId).IsUnique();
            e.HasIndex(x => x.FileId).IsUnique();
            e.Property(x => x.SignDate).HasColumnType("date");
        });
        model.Entity<TblContractHandoverPlaceholderValue>(e =>
        {
            e.ToTable("tbl_ContractHandoverPlaceholderValue");
            e.HasKey(x => x.HandoverPlaceholderValueId);
            e.HasIndex(x => new { x.HandoverRecordId, x.PlaceholderKey }).IsUnique();
            e.Property(x => x.PlaceholderKey).HasMaxLength(100);
            e.Property(x => x.SourceFieldKey).HasMaxLength(250);
            e.Property(x => x.RenderedValue).HasColumnType("nvarchar(max)");
        });
        model.Entity<TblContractAcceptanceScopeAllocation>(e =>
        {
            e.ToTable("tbl_ContractAcceptanceScopeAllocation", t => t.HasCheckConstraint("CK_AcceptanceScopeAllocation_Quantity", "[Quantity] > 0"));
            e.HasKey(x => x.AcceptanceScopeAllocationId);
            e.HasIndex(x => new { x.AcceptanceRecordId, x.HandoverLineId }).IsUnique();
            e.HasIndex(x => x.HandoverLineId);
            e.Property(x => x.Quantity).HasPrecision(18, 4);
        });
    }

    internal static void ConfigurePlaceholders(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblContractTemplateVersion>().Property(x => x.PlaceholderBindingHash).HasMaxLength(64).IsUnicode(false);
        modelBuilder.Entity<TblContractPlaceholderDefinition>(e =>
        {
            e.ToTable("tbl_ContractPlaceholderDefinition");
            e.HasKey(x => x.PlaceholderDefinitionId);
            e.HasIndex(x => x.PlaceholderKey).IsUnique();
            e.Property(x => x.PlaceholderKey).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.FieldLabel).HasMaxLength(300);
            e.Property(x => x.SourceFieldKey).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.DefaultValue).HasMaxLength(2000);
            e.Property(x => x.FormatString).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        modelBuilder.Entity<TblContractTemplateField>(e =>
        {
            e.Property(x => x.SourceFieldKey).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.DefinitionRowVersion).HasMaxLength(8);
        });
        modelBuilder.Entity<TblContractVersionPlaceholderValue>(e =>
        {
            e.ToTable("tbl_ContractVersionPlaceholderValue");
            e.HasKey(x => x.ContractVersionPlaceholderValueId);
            e.HasIndex(x => new { x.VersionId, x.PlaceholderKey }).IsUnique();
            e.HasIndex(x => new { x.ContractId, x.VersionId });
            e.Property(x => x.PlaceholderKey).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.SourceFieldKey).HasMaxLength(200).IsUnicode(false);
        });
        modelBuilder.Entity<TblContractPlaceholderAudit>(e =>
        {
            e.ToTable("tbl_ContractPlaceholderAudit", table =>
            {
                table.HasCheckConstraint(
                    "CK_tbl_ContractPlaceholderAudit_ActorEmployeeId",
                    "[ActorEmployeeId] > 0");
                table.HasCheckConstraint(
                    "CK_tbl_ContractPlaceholderAudit_ActionType",
                    "[ActionType] IN ('PlaceholderDefinitionCreated', 'PlaceholderDefinitionUpdated', 'PlaceholderDefinitionActivated', 'PlaceholderDefinitionDeactivated', 'PlaceholderDefinitionDeleted')");
                table.HasCheckConstraint(
                    "CK_tbl_ContractPlaceholderAudit_NewSourceFieldKey",
                    "LEN(LTRIM(RTRIM([NewSourceFieldKey]))) > 0");
                table.HasCheckConstraint(
                    "CK_tbl_ContractPlaceholderAudit_Snapshot",
                    "([ActionType] = 'PlaceholderDefinitionCreated' AND [PreviousSourceFieldKey] IS NULL AND [PreviousFormatString] IS NULL AND [PreviousIsActive] IS NULL) OR " +
                    "([ActionType] <> 'PlaceholderDefinitionCreated' AND LEN(LTRIM(RTRIM([PreviousSourceFieldKey]))) > 0)");
            });
            e.HasKey(x => x.PlaceholderAuditId);
            e.HasIndex(x => new { x.PlaceholderKey, x.OccurredAt });
            e.Property(x => x.PlaceholderKey).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.ActionType).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.PreviousSourceFieldKey).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.PreviousFormatString).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.NewSourceFieldKey).HasMaxLength(200).IsUnicode(false);
            e.Property(x => x.NewFormatString).HasMaxLength(100).IsUnicode(false);
            e.Property(x => x.OccurredAt).HasColumnType("datetime2");
        });
    }

    internal static void ConfigureLegalBases(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblContractTemplateLegalBasis>(entity =>
        {
            entity.HasKey(e => e.TemplateLegalBasisId)
                .HasName("PK_tbl_ContractTemplateLegalBasis");
            entity.ToTable("tbl_ContractTemplateLegalBasis", table =>
            {
                table.HasCheckConstraint("CK_tbl_ContractTemplateLegalBasis_TemplateVersionId", "[TemplateVersionId] > 0");
                table.HasCheckConstraint("CK_tbl_ContractTemplateLegalBasis_BasisCode", "LEN(LTRIM(RTRIM([BasisCode]))) > 0");
                table.HasCheckConstraint("CK_tbl_ContractTemplateLegalBasis_ContentVi", "LEN(LTRIM(RTRIM([ContentVi]))) > 0");
                table.HasCheckConstraint("CK_tbl_ContractTemplateLegalBasis_DisplayOrder", "[DisplayOrder] >= 0");
            });
            entity.HasIndex(e => new { e.TemplateVersionId, e.BasisCode })
                .IsUnique()
                .HasDatabaseName("UX_tbl_ContractTemplateLegalBasis_Version_BasisCode");
            entity.HasIndex(e => new { e.TemplateVersionId, e.DisplayOrder })
                .HasDatabaseName("IX_tbl_ContractTemplateLegalBasis_Version_DisplayOrder");
            entity.Property(e => e.BasisCode).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(e => e.ContentVi).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ContentEn).HasColumnType("nvarchar(max)");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0, "DF_tbl_ContractTemplateLegalBasis_DisplayOrder");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2").HasDefaultValueSql("(sysutcdatetime())", "DF_tbl_ContractTemplateLegalBasis_CreatedDate");
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime2");
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
        });

        modelBuilder.Entity<TblContractLegalBasis>(entity =>
        {
            entity.HasKey(e => e.LegalBasisId)
                .HasName("PK_tbl_ContractLegalBasis");
            entity.ToTable("tbl_ContractLegalBasis", table =>
            {
                table.HasCheckConstraint("CK_tbl_ContractLegalBasis_ContractId", "[ContractId] > 0");
                table.HasCheckConstraint("CK_tbl_ContractLegalBasis_VersionId", "[VersionId] > 0");
                table.HasCheckConstraint("CK_tbl_ContractLegalBasis_SourceTemplateLegalBasisId", "[SourceTemplateLegalBasisId] IS NULL OR [SourceTemplateLegalBasisId] > 0");
                table.HasCheckConstraint("CK_tbl_ContractLegalBasis_BasisCode", "LEN(LTRIM(RTRIM([BasisCode]))) > 0");
                table.HasCheckConstraint("CK_tbl_ContractLegalBasis_ContentVi", "LEN(LTRIM(RTRIM([ContentVi]))) > 0");
                table.HasCheckConstraint("CK_tbl_ContractLegalBasis_DisplayOrder", "[DisplayOrder] >= 0");
            });
            entity.HasIndex(e => new { e.VersionId, e.BasisCode })
                .IsUnique()
                .HasDatabaseName("UX_tbl_ContractLegalBasis_Version_BasisCode");
            entity.HasIndex(e => new { e.ContractId, e.VersionId, DisplayOrder = e.DisplayOrder })
                .HasDatabaseName("IX_tbl_ContractLegalBasis_Contract_Version_DisplayOrder");
            entity.HasIndex(e => e.SourceTemplateLegalBasisId)
                .HasDatabaseName("IX_tbl_ContractLegalBasis_SourceTemplateLegalBasisId");
            entity.Property(e => e.BasisCode).HasMaxLength(100).IsUnicode(false).IsRequired();
            entity.Property(e => e.ContentVi).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ContentEn).HasColumnType("nvarchar(max)");
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0, "DF_tbl_ContractLegalBasis_DisplayOrder");
            entity.Property(e => e.CreatedDate).HasColumnType("datetime2").HasDefaultValueSql("(sysutcdatetime())", "DF_tbl_ContractLegalBasis_CreatedDate");
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime2");
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
        });
    }

    internal static void ConfigureContractVersionSnapshots(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblContractVersionLegalSnapshot>(entity =>
        {
            entity.ToTable("tbl_ContractVersionLegalSnapshot", table =>
            {
                table.HasCheckConstraint("CK_tbl_ContractVersionLegalSnapshot_Ids",
                    "[VersionId] > 0 AND [ContractId] > 0 AND [VersionNo] > 0 AND [TemplateVersionId] > 0 AND [CreatedByEmployeeId] > 0");
                table.HasCheckConstraint("CK_tbl_ContractVersionLegalSnapshot_Financials",
                    "[Subtotal] >= 0 AND [TotalDiscount] >= 0 AND [TotalVat] >= 0 AND [TotalAmount] >= 0");
            });
            entity.HasKey(x => x.ContractVersionLegalSnapshotId);
            entity.HasIndex(x => x.VersionId).IsUnique();
            entity.Property(x => x.ContractCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.ContractName).HasMaxLength(1000);
            entity.Property(x => x.ContractNameEn).HasMaxLength(500).IsUnicode(false);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3).IsFixedLength().IsUnicode(false);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.TotalDiscount).HasPrecision(18, 2);
            entity.Property(x => x.TotalVat).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.ContractCreatedDate).HasColumnType("datetime2");
            entity.Property(x => x.SignDate).HasColumnType("datetime2");
            entity.Property(x => x.EffectiveDate).HasColumnType("datetime2");
            entity.Property(x => x.ExpireDate).HasColumnType("datetime2");
            entity.Property(x => x.CreatedDate).HasColumnType("datetime2");
            entity.HasOne(x => x.Version).WithOne(x => x.LegalSnapshot)
                .HasForeignKey<TblContractVersionLegalSnapshot>(x => x.VersionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TblContractVersionPartySnapshot>(entity =>
        {
            entity.ToTable("tbl_ContractVersionPartySnapshot", table =>
            {
                table.HasCheckConstraint("CK_tbl_ContractVersionPartySnapshot_Role", "[PartyRole] IN (1, 2)");
                table.HasCheckConstraint("CK_tbl_ContractVersionPartySnapshot_Source",
                    "([PartyRole] = 1 AND [SourceCustomerId] IS NULL) OR ([PartyRole] = 2 AND [SourceCustomerId] > 0)");
            });
            entity.HasKey(x => x.ContractVersionPartySnapshotId);
            entity.HasIndex(x => new { x.ContractVersionLegalSnapshotId, x.PartyRole }).IsUnique();
            entity.Property(x => x.LegalName).HasMaxLength(1000);
            entity.Property(x => x.TaxCode).HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.Address).HasMaxLength(2000);
            entity.Property(x => x.RepresentativeName).HasMaxLength(500);
            entity.Property(x => x.RepresentativeTitle).HasMaxLength(500);
            entity.Property(x => x.PhoneNumber).HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.FaxNumber).HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.BankAccountNumber).HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.BankName).HasMaxLength(500);
            entity.HasOne(x => x.LegalSnapshot).WithMany(x => x.Parties)
                .HasForeignKey(x => x.ContractVersionLegalSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TblContractVersionPaymentMilestoneSnapshot>(entity =>
        {
            entity.ToTable("tbl_ContractVersionPaymentMilestoneSnapshot", table =>
            {
                table.HasCheckConstraint("CK_tbl_ContractVersionPaymentMilestoneSnapshot_Ids",
                    "[SourcePaymentMilestoneId] > 0 AND [SourceTermId] > 0 AND ([SourceTemplatePaymentMilestoneId] IS NULL OR [SourceTemplatePaymentMilestoneId] > 0)");
                table.HasCheckConstraint("CK_tbl_ContractVersionPaymentMilestoneSnapshot_Values",
                    "[PaymentPercent] > 0 AND [PaymentPercent] <= 100 AND [Amount] >= 0 AND [DisplayOrder] > 0");
            });
            entity.HasKey(x => x.ContractVersionPaymentMilestoneSnapshotId);
            entity.HasIndex(x => new { x.ContractVersionLegalSnapshotId, x.SourcePaymentMilestoneId }).IsUnique();
            entity.HasIndex(x => new { x.ContractVersionLegalSnapshotId, x.DisplayOrder });
            entity.Property(x => x.MilestoneCode).HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.TitleVi).HasMaxLength(500);
            entity.Property(x => x.TitleEn).HasMaxLength(500).IsUnicode(false);
            entity.Property(x => x.PaymentPercent).HasPrecision(9, 4);
            entity.Property(x => x.ConditionVi).HasMaxLength(2000);
            entity.Property(x => x.ConditionEn).HasMaxLength(2000).IsUnicode(false);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.AnchorDate).HasColumnType("datetime2");
            entity.Property(x => x.DueDate).HasColumnType("datetime2");
            entity.Property(x => x.PaidAt).HasColumnType("datetime2");
            entity.HasOne(x => x.LegalSnapshot).WithMany(x => x.PaymentMilestones)
                .HasForeignKey(x => x.ContractVersionLegalSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    internal static void ConfigureBilingual(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblContract>(entity =>
        {
            entity.Property(x => x.ContractDirection).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_tbl_Contract_ContractDirection", "[ContractDirection] IN (1, 2)"));
            entity.HasIndex(x => new { x.ContractDirection, x.Status });
        });
        modelBuilder.Entity<TblContractTemplateVersion>(entity =>
        {
            entity.Property(x => x.ContractDirection).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_tbl_ContractTemplateVersion_ContractDirection", "[ContractDirection] IN (1, 2)"));
            entity.HasIndex(x => new { x.ContractDirection, x.Status });
        });
        modelBuilder.Entity<TblContractVersionLegalSnapshot>(entity =>
        {
            entity.Property(x => x.ContractDirection).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint("CK_tbl_ContractVersionLegalSnapshot_ContractDirection", "[ContractDirection] IN (1, 2)"));
        });
        modelBuilder.Entity<TblProduct>(entity =>
        {
            entity.Property(x => x.ProductNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_Products_ProductNameEn",
                "LEN(LTRIM(RTRIM([ProductNameEn]))) > 0"));
        });
        modelBuilder.Entity<TblService>(entity =>
        {
            entity.Property(x => x.ServiceNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_Services_ServiceNameEn",
                "LEN(LTRIM(RTRIM([ServiceNameEn]))) > 0"));
        });
        modelBuilder.Entity<TblContractTemplateTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractTemplateTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractTemplateAppendixTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractTemplateAppendixTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractAppendixTerm>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractAppendixTerm_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [TermContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblContractAcceptanceSection>(entity =>
        {
            entity.Property(x => x.BilingualContentMode).HasDefaultValue((byte)1);
            entity.ToTable(table => table.HasCheckConstraint("CK_TblContractAcceptanceSection_BilingualContentMode",
                "[BilingualContentMode] IN (1, 2) AND ([BilingualContentMode] <> 2 OR [ContentEn] IS NULL)"));
        });
        modelBuilder.Entity<TblTenantLegalProfile>(entity =>
        {
            entity.Property(x => x.LegalEntityNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_LegalEntityNameEn",
                "LEN(LTRIM(RTRIM([LegalEntityNameEn]))) > 0"));
            entity.Property(x => x.AddressEn).HasMaxLength(2000).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_AddressEn",
                "LEN(LTRIM(RTRIM([AddressEn]))) > 0"));
            entity.Property(x => x.RepresentativeTitleEn).HasMaxLength(200).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_RepresentativeTitleEn",
                "LEN(LTRIM(RTRIM([RepresentativeTitleEn]))) > 0"));
            entity.Property(x => x.BankNameEn).HasMaxLength(500).IsUnicode().IsRequired(false);
            entity.ToTable(table => table.HasCheckConstraint("CK_TenantLegalProfile_BankNameEn",
                "LEN(LTRIM(RTRIM([BankNameEn]))) > 0"));
        });
        modelBuilder.Entity<TblContractVersionPartySnapshot>(entity =>
        {
            entity.Property(x => x.SubjectCode).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.ContactName).HasMaxLength(300).IsUnicode();
            entity.Property(x => x.ContactTitle).HasMaxLength(300).IsUnicode();
            entity.Property(x => x.ContactPhone).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.FullName).HasMaxLength(300).IsUnicode();
            entity.Property(x => x.Email).HasMaxLength(255).IsUnicode();
            entity.Property(x => x.Website).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.City).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.Country).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.Mobile).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.ZipCode).HasMaxLength(50).IsUnicode();
            entity.Property(x => x.LegalNameEn).HasMaxLength(1000).IsUnicode();
            entity.Property(x => x.AddressEn).HasMaxLength(2000).IsUnicode();
            entity.Property(x => x.RepresentativeTitleEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.ContactTitleEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.BankNameEn).HasMaxLength(1000).IsUnicode();
            entity.Property(x => x.CityEn).HasMaxLength(100).IsUnicode();
            entity.Property(x => x.CountryEn).HasMaxLength(100).IsUnicode();
        });
        modelBuilder.Entity<TblContractAcceptanceParty>(entity =>
        {
            entity.Property(x => x.LegalNameEn).HasMaxLength(500).IsUnicode();
            entity.Property(x => x.AddressEn).HasMaxLength(2000).IsUnicode();
            entity.Property(x => x.RepresentativeTitleEn).HasMaxLength(300).IsUnicode();
        });
        modelBuilder.Entity<TblContractAcceptanceRecord>().Property(x => x.ContractSignDate).HasColumnType("date");
        modelBuilder.Entity<TblContractAcceptanceRecord>().Property(x => x.LocationEn)
            .HasMaxLength(500).IsUnicode();
    }
}

