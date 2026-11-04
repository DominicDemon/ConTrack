using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

public partial class DbDtctechContext
{
    public virtual DbSet<TblTicketSupport> TblTicketSupports { get; set; } = null!;
    public virtual DbSet<TblTicketMessage> TblTicketMessages { get; set; } = null!;
    public virtual DbSet<TblTicketSupportEmployee> TblTicketSupportEmployees { get; set; } = null!;

    public static void ConfigureSupportTickets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblTicketSupport>(entity =>
        {
            entity.ToTable("TICKET_SUPPORT");
            entity.HasKey(e => e.TicketSupportId);

            entity.Property(e => e.TicketSupportId).HasColumnName("ticket_support_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.TenantId).HasColumnName("tenant_id");
            entity.Property(e => e.DepartmentId).HasColumnName("department_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.ServiceId).HasColumnName("service_id");
            entity.Property(e => e.TicketCode).HasColumnName("ticket_code").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.TicketTitle).HasColumnName("ticket_title").HasMaxLength(1000);
            entity.Property(e => e.TicketDescription).HasColumnName("ticket_description");
            entity.Property(e => e.TicketPriority).HasColumnName("ticket_priority").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TicketStatus).HasColumnName("ticket_status").HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.TicketResolution).HasColumnName("ticket_resolution");
            entity.Property(e => e.TicketResolvedAt).HasColumnName("ticket_resolved_at");
            entity.Property(e => e.TicketClosedAt).HasColumnName("ticket_closed_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

            entity.HasMany(e => e.Messages)
                  .WithOne(m => m.TicketSupport)
                  .HasForeignKey(m => m.TicketSupportId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.AssignedEmployees)
                  .WithOne(a => a.TicketSupport)
                  .HasForeignKey(a => a.TicketSupportId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TblTicketMessage>(entity =>
        {
            entity.ToTable("TICKET_MESSAGE");
            entity.HasKey(e => e.TicketMessageId);

            entity.Property(e => e.TicketMessageId).HasColumnName("ticket_message_id");
            entity.Property(e => e.TicketSupportId).HasColumnName("ticket_support_id");
            entity.Property(e => e.TicketMessageSenderCustomerId).HasColumnName("ticket_message_sender_customer_id");
            entity.Property(e => e.TicketMessageSenderEmployeeId).HasColumnName("ticket_message_sender_employee_id");
            entity.Property(e => e.TicketMessageContent).HasColumnName("ticket_message_content");
            entity.Property(e => e.TicketMessageType).HasColumnName("ticket_message_type").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TicketMessageAttachmentUrl).HasColumnName("ticket_message_attachment_url").HasMaxLength(1000).IsUnicode(false);
            entity.Property(e => e.TicketMessageAttachmentName).HasColumnName("ticket_message_attachment_name").HasMaxLength(1000);
            entity.Property(e => e.TicketMessageAttachmentType).HasColumnName("ticket_message_attachment_type").HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<TblTicketSupportEmployee>(entity =>
        {
            entity.ToTable("TICKET_SUPPORT_EMPLOYEE");
            entity.HasKey(e => e.TicketSupportEmployeeId);

            entity.Property(e => e.TicketSupportEmployeeId).HasColumnName("ticket_support_employee_id");
            entity.Property(e => e.TicketSupportId).HasColumnName("ticket_support_id");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.TicketSupportEmployeeAssignmentRole).HasColumnName("ticket_support_employee_assignment_role").HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.TicketSupportEmployeeAssignedAt).HasColumnName("ticket_support_employee_assigned_at");
            entity.Property(e => e.TicketSupportEmployeeUnassignedAt).HasColumnName("ticket_support_employee_unassigned_at");
            entity.Property(e => e.TicketSupportEmployeeAssignedByEmployeeId).HasColumnName("ticket_support_employee_assigned_by_employee_id");
        });
    }
}

