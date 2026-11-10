using System;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

public partial class DbDtctechContext
{
    public virtual DbSet<TICKET_SUPPORT> TICKET_SUPPORT { get; set; } = null!;
    public virtual DbSet<TICKET_MESSAGE> TICKET_MESSAGE { get; set; } = null!;
    public virtual DbSet<TICKET_ATTACHMENTS> TICKET_ATTACHMENTS { get; set; } = null!;
    public virtual DbSet<TICKET_HISTORY> TICKET_HISTORY { get; set; } = null!;
    public virtual DbSet<TICKET_SUPPORT_EMPLOYEE> TICKET_SUPPORT_EMPLOYEE { get; set; } = null!;
    public virtual DbSet<TICKET_TYPES> TICKET_TYPES { get; set; } = null!;

    public static void ConfigureSupportTickets(ModelBuilder modelBuilder)
    {
        // 1. DEPARTMENT -> [DEPARTMENT] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<DEPARTMENT>(entity =>
        {
            entity.ToTable("DEPARTMENT");
            entity.HasKey(e => e.DepartmentId);
            entity.Property(e => e.DepartmentId)
                .HasColumnName("department_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.DepartmentCode)
                .HasColumnName("department_code")
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.DepartmentName)
                .HasColumnName("department_name")
                .HasMaxLength(200);
            entity.Property(e => e.ModifiedDate)
                .HasColumnName("updated_at")
                .HasColumnType("datetime2");
            entity.Property(e => e.Stutus)
                .HasColumnName("department_status")
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasConversion(
                    v => v == 0 ? "INACTIVE" : "ACTIVE",
                    v => string.IsNullOrWhiteSpace(v)
                        || string.Equals(v, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                        || v == "1"
                        ? (byte?)1
                        : (byte?)0);
            entity.Property(e => e.LangId)
                .HasColumnName("created_by")
                .HasConversion<long?>();
        });

        // 2. EMPLOYEE -> [EMPLOYEE] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<EMPLOYEE>(entity =>
        {
            entity.ToTable("EMPLOYEE");
            entity.HasKey(e => e.EmployeeId);
            entity.Property(e => e.EmployeeId)
                .HasColumnName("employee_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.EmployeeCode)
                .HasColumnName("employee_code")
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.EmployeeAccount)
                .HasColumnName("employee_username")
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EmployeePassword)
                .HasColumnName("employee_password_hash")
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.EmployeeFullName)
                .HasColumnName("employee_display_name")
                .HasMaxLength(200);
            entity.Property(e => e.EmployeeBirthDate)
                .HasColumnName("employee_dob")
                .HasColumnType("date");
            entity.Property(e => e.Gender)
                .HasColumnName("employee_gender")
                .HasMaxLength(10)
                .IsUnicode(false)
                .IsFixedLength(false);
            entity.Property(e => e.EmployeeMobile)
                .HasColumnName("employee_phone")
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.EmployeePhone)
                .HasColumnName("employee_personal_identifier")
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.EmployeeEmail)
                .HasColumnName("employee_email")
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.RecoveryEmailVerifiedAt)
                .HasColumnName("employee_email_verified_at")
                .HasColumnType("datetime2");
            entity.Property(e => e.EmployeeAddress)
                .HasColumnName("employee_address")
                .HasMaxLength(1000);
            entity.Property(e => e.UserCreated)
                .HasColumnName("created_by")
                .HasConversion<long?>();
            entity.Property(e => e.UserModified)
                .HasColumnName("updated_by")
                .HasConversion<long?>();
            entity.Property(e => e.DateCreated)
                .HasColumnName("created_at")
                .HasColumnType("datetime2");
            entity.Property(e => e.DateModified)
                .HasColumnName("updated_at")
                .HasColumnType("datetime2");
            entity.Property(e => e.Status)
                .HasColumnName("employee_status")
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasConversion(
                    v => v == 0 ? "INACTIVE" : "ACTIVE",
                    v => string.IsNullOrWhiteSpace(v)
                        || string.Equals(v, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                        || v == "1"
                        ? (byte?)1
                        : (byte?)0);
            entity.Property(e => e.PasswordChangedAt)
                .HasColumnName("employee_last_login_at")
                .HasColumnType("datetime2");

            entity.Ignore(e => e.TitleId);
            entity.Ignore(e => e.MaritalStatus);
            entity.Ignore(e => e.HireDate);
            entity.Ignore(e => e.DepartmentId);
            entity.Ignore(e => e.Others);
            entity.Ignore(e => e.DefaultPage);
            entity.Ignore(e => e.EmployeeImageIcon);
            entity.Ignore(e => e.EmployeeType);
            entity.Ignore(e => e.UserRoles);
            entity.Ignore(e => e.WorkTypeId);
            entity.Ignore(e => e.MustChangePassword);
            entity.Ignore(e => e.SessionVersion);
            entity.Ignore(e => e.AvatarStorageKey);
            entity.Ignore(e => e.AvatarContentType);
            entity.Ignore(e => e.AvatarFileSize);
            entity.Ignore(e => e.AvatarSha256);
            entity.Ignore(e => e.AvatarUpdatedAt);
            entity.Ignore(e => e.CoverStorageKey);
            entity.Ignore(e => e.CoverContentType);
            entity.Ignore(e => e.CoverFileSize);
            entity.Ignore(e => e.CoverSha256);
            entity.Ignore(e => e.CoverUpdatedAt);
            entity.Ignore(e => e.RowVersion);
        });

        // 3. CUSTOMER -> [CUSTOMER] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<CUSTOMER>(entity =>
        {
            entity.ToTable("CUSTOMER");
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.CustomerId)
                .HasColumnName("customer_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.CustomerFullName)
                .HasColumnName("customer_display_name")
                .HasMaxLength(200);
            entity.Property(e => e.CustomerAddress)
                .HasColumnName("customer_address")
                .HasMaxLength(1000);
            entity.Property(e => e.CustomerTaxCode)
                .HasColumnName("customer_personal_tax_identifier")
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CustomerPhone)
                .HasColumnName("customer_personal_phone")
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CustomerPassword)
                .HasColumnName("customer_password_hash")
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CustomerEmail)
                .HasColumnName("customer_email")
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.CustomerBirthday)
                .HasColumnName("customer_dob")
                .HasColumnType("date");
            entity.Property(e => e.Status)
                .HasColumnName("customer_status")
                .HasConversion<int?>();
            entity.Property(e => e.DateCreated)
                .HasColumnName("created_at")
                .HasColumnType("datetime2");
            entity.Property(e => e.DateModified)
                .HasColumnName("updated_at")
                .HasColumnType("datetime2");
            entity.Property(e => e.UserCreated)
                .HasColumnName("created_by")
                .HasConversion<long?>();
            entity.Property(e => e.UserModified)
                .HasColumnName("updated_by")
                .HasConversion<long?>();

            entity.Ignore(e => e.CustomerCompanyEn);
            entity.Ignore(e => e.CustomerAddressEn);
            entity.Ignore(e => e.CustomerBankNameEn);
            entity.Ignore(e => e.CustomerCityEn);
            entity.Ignore(e => e.CustomerCountryEn);
            entity.Ignore(e => e.CustomerContactPersonTitleEn);
            entity.Ignore(e => e.CustomerRepresentativeTitleEn);
            entity.Ignore(e => e.CustomerCode);
            entity.Ignore(e => e.CustomerAccount);
            entity.Ignore(e => e.CustomerCompany);
            entity.Ignore(e => e.CustomerMobile);
            entity.Ignore(e => e.CustomerFaxNumber);
            entity.Ignore(e => e.CustomerRepresentativeName);
            entity.Ignore(e => e.CustomerRepresentativeTitle);
            entity.Ignore(e => e.CustomerContactPersonName);
            entity.Ignore(e => e.CustomerContactPersonPhone);
            entity.Ignore(e => e.CustomerContactPersonTitle);
            entity.Ignore(e => e.CustomerBankAccountNumber);
            entity.Ignore(e => e.CustomerBankName);
            entity.Ignore(e => e.CustomerCity);
            entity.Ignore(e => e.CustomerZipCode);
            entity.Ignore(e => e.CustomerCountry);
            entity.Ignore(e => e.CustomerPoints);
            entity.Ignore(e => e.CustomerCardId);
            entity.Ignore(e => e.CustomerRegion);
            entity.Ignore(e => e.CustomerComments);
            entity.Ignore(e => e.CustomerImageIcon);
            entity.Ignore(e => e.CustomerWebsite);
            entity.Ignore(e => e.CustomerSourceId);
            entity.Ignore(e => e.CustomerParentId);
            entity.Ignore(e => e.CustomerNotes);
            entity.Ignore(e => e.CustomerCareerId);
        });

        // 4. CATEGORIES -> [CATEGORIES] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<CATEGORIES>(entity =>
        {
            entity.ToTable("CATEGORIES");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryId)
                .HasColumnName("category_id")
                .HasConversion<int>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.CategoryName).HasColumnName("category_name").HasMaxLength(500);
            entity.Property(e => e.CategoryShortDesc).HasColumnName("category_short_desc").HasMaxLength(1000);
            entity.Property(e => e.Image).HasColumnName("category_image").HasMaxLength(50);
            entity.Property(e => e.CategoryParentId)
                .HasColumnName("category_parentid")
                .HasConversion<int?>();
            entity.Property(e => e.CategoryOrder)
                .HasColumnName("category_sortorder")
                .HasConversion<int?>();
            entity.Ignore(e => e.LangId);
        });

        // 5. PRODUCTS -> [PRODUCTS] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<PRODUCTS>(entity =>
        {
            entity.ToTable("PRODUCTS");
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductId)
                .HasColumnName("product_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.ProductCode).HasColumnName("product_code").HasMaxLength(20);
            entity.Property(e => e.ProductName).HasColumnName("product_name").HasMaxLength(500);
            entity.Property(e => e.ProductNameEn).HasColumnName("product_nameen").HasMaxLength(500);
            entity.Property(e => e.ProductShortName).HasColumnName("product_shortname").HasMaxLength(100);
            entity.Property(e => e.UnitName).HasColumnName("product_unitname").HasMaxLength(100);
            entity.Property(e => e.UnitNameEn).HasColumnName("product_unitnameen").HasMaxLength(100);
            entity.Property(e => e.ProductSlogan).HasColumnName("product_slogan").HasMaxLength(400);
            entity.Property(e => e.ProductShortDesc).HasColumnName("product_shortdesc").HasMaxLength(2000);
            entity.Property(e => e.ProductDetails).HasColumnName("product_details");
            entity.Property(e => e.ProductLargeImage).HasColumnName("product_largeimage").HasMaxLength(500);
            entity.Property(e => e.ProductSmallImage).HasColumnName("product_smallimage").HasMaxLength(500);
            entity.Property(e => e.ProductIconClass).HasColumnName("product_iconclass").HasMaxLength(50);
            entity.Property(e => e.ProductDocument).HasColumnName("product_document").HasMaxLength(200);
            entity.Property(e => e.ProductTags).HasColumnName("product_tags").HasMaxLength(500);
            entity.Property(e => e.ProductDeployment).HasColumnName("product_deployment").HasMaxLength(4000);
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.ProductPrice)
                .HasColumnName("product_suggested_price")
                .HasColumnType("decimal(18,2)")
                .HasConversion<decimal?>();
            entity.Property(e => e.ViewTotal).HasColumnName("product_quantity");
            entity.Property(e => e.ProductOrder).HasColumnName("product_sortorder");
            entity.Property(e => e.Rewrite).HasColumnName("product_rewrite").HasMaxLength(200);
            entity.Property(e => e.TitleBrowser).HasColumnName("product_titlebrowser").HasMaxLength(500);
            entity.Property(e => e.MetaDescription).HasColumnName("product_metadescription").HasMaxLength(2000);
            entity.Property(e => e.MetaKeyword).HasColumnName("product_metakeyword").HasMaxLength(2000);
            entity.Property(e => e.Others).HasColumnName("product_others").HasMaxLength(4000);
            entity.Property(e => e.ProductCreatedDate).HasColumnName("product_created_at").HasColumnType("datetime");

            entity.Ignore(e => e.ProductFeatures);
            entity.Ignore(e => e.ProductBenefit);
            entity.Ignore(e => e.LangId);
            entity.Ignore(e => e.Status);
            entity.Ignore(e => e.Customers);
            entity.Ignore(e => e.ProductOverall);
            entity.Ignore(e => e.DisplaySlide);
            entity.Ignore(e => e.GetTrialVersion);
            entity.Ignore(e => e.GoogleClick);
        });

        // 6. SERVICE_TYPES -> [SERVICE_TYPES] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<SERVICE_TYPES>(entity =>
        {
            entity.ToTable("SERVICE_TYPES");
            entity.HasKey(e => e.ServiceTypeId);
            entity.Property(e => e.ServiceTypeId)
                .HasColumnName("servicetype_id")
                .HasConversion<int>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.ServiceTypeName).HasColumnName("servicetype_name").HasMaxLength(200);
            entity.Ignore(e => e.LangId);
        });

        // 7. SERVICES -> [SERVICES] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<SERVICES>(entity =>
        {
            entity.ToTable("SERVICES");
            entity.HasKey(e => e.ServiceId);
            entity.Property(e => e.ServiceId)
                .HasColumnName("service_id")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.ServiceName).HasColumnName("service_name").HasMaxLength(2000);
            entity.Property(e => e.ServiceNameEn).HasColumnName("service_nameen").HasMaxLength(500);
            entity.Property(e => e.UnitName).HasColumnName("service_unitname").HasMaxLength(100);
            entity.Property(e => e.UnitNameEn).HasColumnName("service_unitnameen").HasMaxLength(100);
            entity.Property(e => e.ServicePrice)
                .HasColumnName("service_price")
                .HasColumnType("decimal(18,2)")
                .HasConversion<decimal?>();
            entity.Property(e => e.ServiceTypeId)
                .HasColumnName("servicetype_id")
                .HasConversion<int?>();
            entity.Property(e => e.ServiceRegion)
                .HasColumnName("service_region")
                .HasConversion<int?>();
            entity.Property(e => e.ServiceImageIcon).HasColumnName("service_imageicon").HasMaxLength(50);
            entity.Property(e => e.Ftpaccounts)
                .HasColumnName("service_fptaccounts")
                .HasConversion<long?>();
            entity.Property(e => e.MySql)
                .HasColumnName("service_mysql")
                .HasConversion<int?>();
            entity.Property(e => e.HasChild).HasColumnName("service_haschild");
            entity.Property(e => e.Rewrite).HasColumnName("service_rewrite").HasMaxLength(300);
            entity.Property(e => e.TitleBrowser).HasColumnName("service_titlebrowser").HasMaxLength(500);
            entity.Property(e => e.MetaDescription).HasColumnName("service_metadescription").HasMaxLength(2000);
            entity.Property(e => e.MetaKeyword).HasColumnName("service_metakeyword").HasMaxLength(2000);
            entity.Property(e => e.Others).HasColumnName("service_others").HasMaxLength(4000);
            entity.Property(e => e.Status)
                .HasColumnName("service_status")
                .HasConversion<int?>();
            entity.Property(e => e.DateCreated).HasColumnName("service_datecreated").HasColumnType("datetime");
            entity.Property(e => e.DateModified).HasColumnName("service_datemodified").HasColumnType("datetime");

            entity.Ignore(e => e.ServiceParentId);
            entity.Ignore(e => e.Points);
            entity.Ignore(e => e.UserCreated);
            entity.Ignore(e => e.UserModified);
            entity.Ignore(e => e.DiskStorage);
            entity.Ignore(e => e.Bandwidth);
            entity.Ignore(e => e.SubDomain);
            entity.Ignore(e => e.EmailAccounts);
            entity.Ignore(e => e.MsSqlServer);
            entity.Ignore(e => e.ParkDomain);
            entity.Ignore(e => e.ServicePackageId);
            entity.Ignore(e => e.LangId);
            entity.Ignore(e => e.ServiceGroupId);
            entity.Ignore(e => e.SetupPrice);
            entity.Ignore(e => e.MaintainPrice);
            entity.Ignore(e => e.ServiceContent);
            entity.Ignore(e => e.ServiceOrder);
            entity.Ignore(e => e.ServiceShortDesc);
            entity.Ignore(e => e.EmailForwarders);
        });

        // 8. FILE_STORAGE -> [FILE_STORAGE] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<FILE_STORAGE>(entity =>
        {
            entity.ToTable("FILE_STORAGE");
            entity.HasKey(e => e.FileId);
            entity.Property(e => e.FileId)
                .HasColumnName("file_storage_fileid")
                .HasConversion<long>()
                .ValueGeneratedOnAdd();
            entity.Property(e => e.TenantCode).HasColumnName("file_storage_tenantcode").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.ObjectId)
                .HasColumnName("file_storage_objectid")
                .HasConversion<long>();
            entity.Property(e => e.ObjectType).HasColumnName("file_storage_objecttype").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.FileType).HasColumnName("file_storage_filetype").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.FileName).HasColumnName("file_storage_filename").HasMaxLength(300);
            entity.Property(e => e.FilePath).HasColumnName("file_storage_filepath").HasMaxLength(1000);
            entity.Property(e => e.StorageKey).HasColumnName("file_storage_storagekey").HasMaxLength(1000).IsUnicode(false);
            entity.Property(e => e.ContentType).HasColumnName("file_storage_contenttype").HasMaxLength(255).IsUnicode(false);
            entity.Property(e => e.FileSize).HasColumnName("file_storage_filesize");
            entity.Property(e => e.Sha256).HasColumnName("file_storage_sha256").HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.UploadedByUserId)
                .HasColumnName("file_storage_uploadedbyemployeeid")
                .HasConversion<long?>();
            entity.Property(e => e.UploadedDate).HasColumnName("file_storage_uploadeddate").HasColumnType("datetime");
        });

        // 9. NOTIFICATION -> [NOTIFICATION] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<NOTIFICATION>(entity =>
        {
            entity.ToTable("NOTIFICATION");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.NotificationId).HasColumnName("notification_id").ValueGeneratedOnAdd();
            entity.Property(e => e.EventKey).HasColumnName("notification_eventkey").HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.ModuleCode).HasColumnName("notification_modulecode").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.EventCode).HasColumnName("notification_eventcode").HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.SubjectType).HasColumnName("notification_subjecttype").HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.SubjectId)
                .HasColumnName("notification_subjectid")
                .HasConversion<long>();
            entity.Property(e => e.Title).HasColumnName("notification_title").HasMaxLength(300);
            entity.Property(e => e.Body).HasColumnName("notification_body").HasMaxLength(1000);
            entity.Property(e => e.ActionKey).HasColumnName("notification_actionkey").HasMaxLength(80).IsUnicode(false);
            entity.Property(e => e.ActorEmployeeId)
                .HasColumnName("employee_id")
                .HasConversion<long?>();
            entity.Property(e => e.CreatedAtUtc).HasColumnName("notification_createdatutc").HasColumnType("datetime2");
            entity.Ignore(e => e.OccurredAtUtc);
        });

        // 10. NOTIFICATION_RECIPIENT -> [NOTIFICATION_RECIPIENT] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<NOTIFICATION_RECIPIENT>(entity =>
        {
            entity.ToTable("NOTIFICATION_RECIPIENT");
            entity.HasKey(e => e.NotificationRecipientId);
            entity.Property(e => e.NotificationRecipientId).HasColumnName("notification_recipient_id").ValueGeneratedOnAdd();
            entity.Property(e => e.NotificationId).HasColumnName("notification_id");
            entity.Property(e => e.EmployeeId)
                .HasColumnName("employee_id")
                .HasConversion<long>();
            entity.Property(e => e.CreatedAtUtc).HasColumnName("notification_recipient_createdatutc").HasColumnType("datetime2");
            entity.Property(e => e.ReadAtUtc).HasColumnName("notification_recipient_readatutc").HasColumnType("datetime2");
        });

        // 11. AUTHORIZATION_AUDIT -> [AUDIT_LOG] in MasterDB-Optimize_v1.sql
        modelBuilder.Entity<AUTHORIZATION_AUDIT>(entity =>
        {
            entity.ToTable("AUDIT_LOG");
            entity.HasKey(x => x.AuthorizationAuditId);
            entity.Property(x => x.AuthorizationAuditId).HasColumnName("audit_log_id");
            entity.Property(x => x.Action).HasColumnName("action_type").HasMaxLength(50).IsUnicode(false);
            entity.Property(x => x.TargetType).HasColumnName("entity_name").HasMaxLength(100).IsUnicode(false);
            entity.Property(x => x.TargetId)
                .HasColumnName("entity_id")
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasConversion(
                    v => string.IsNullOrEmpty(v) ? "N/A" : v,
                    v => v);
            entity.Property(x => x.ActorType).HasColumnName("actor_type").HasMaxLength(20).IsUnicode(false);
            entity.Property(x => x.ActorEmployeeId).HasColumnName("actor_id").HasConversion<long?>();
            entity.Property(x => x.IpAddress).HasColumnName("actor_ip_address").HasMaxLength(45).IsUnicode(false);
            entity.Property(x => x.ChangedFields).HasColumnName("new_data");
            entity.Property(x => x.OccurredAt).HasColumnName("created_at").HasColumnType("datetime2");

            entity.Ignore(x => x.TenantId);
            entity.Ignore(x => x.Result);
            entity.Ignore(x => x.FailureCode);
            entity.Ignore(x => x.PreviousEmployeeType);
            entity.Ignore(x => x.NewEmployeeType);
            entity.Ignore(x => x.PreviousStatus);
            entity.Ignore(x => x.NewStatus);
            entity.Ignore(x => x.UserAgent);
            entity.Ignore(x => x.CorrelationId);
        });
    }
}
