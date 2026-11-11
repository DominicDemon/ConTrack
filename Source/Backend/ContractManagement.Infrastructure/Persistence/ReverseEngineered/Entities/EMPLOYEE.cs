using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class EMPLOYEE
{
    public long employee_id { get; set; }

    public string employee_code { get; set; } = null!;

    public string? employee_email { get; set; }

    public string? employee_phone { get; set; }

    public string employee_password_hash { get; set; } = null!;

    public string employee_display_name { get; set; } = null!;

    public string employee_username { get; set; } = null!;

    public string? employee_personal_identifier { get; set; }

    public string employee_status { get; set; } = null!;

    public DateOnly? employee_dob { get; set; }

    public string? employee_gender { get; set; }

    public string? employee_address { get; set; }

    public string? employee_first_name { get; set; }

    public string? employee_middle_name { get; set; }

    public string? employee_last_name { get; set; }

    public DateTime? employee_email_verified_at { get; set; }

    public DateTime? employee_hand_phone_verified_at { get; set; }

    public DateTime? employee_last_login_at { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
