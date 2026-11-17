using System;
using System.Collections.Generic;

namespace ContractManagement.Infrastructure.Persistence.ReverseEngineered.Entities;

public partial class CUSTOMER
{
    public long customer_id { get; set; }

    public string? customer_email { get; set; }

    public string customer_personal_phone { get; set; } = null!;

    public string customer_password_hash { get; set; } = null!;

    public string customer_display_name { get; set; } = null!;

    public string? customer_personal_tax_identifier { get; set; }

    public int customer_status { get; set; }

    public DateOnly? customer_dob { get; set; }

    public string? customer_gender { get; set; }

    public string? customer_address { get; set; }

    public string? customer_firstname { get; set; }

    public string? customer_middlename { get; set; }

    public string? customer_lastname { get; set; }

    public DateTime? customer_email_verified_at { get; set; }

    public DateTime? customer_personal_phone_verified_at { get; set; }

    public DateTime? customer_last_login_at { get; set; }

    public DateTime created_at { get; set; }

    public long? created_by { get; set; }

    public DateTime? updated_at { get; set; }

    public long? updated_by { get; set; }
}
