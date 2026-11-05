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
        // Tên bảng và tên trường khớp trực tiếp 100% với database (MasterDB-Optimize_v1.sql)
        // Không cần cấu hình ánh xạ thủ công.
    }
}
