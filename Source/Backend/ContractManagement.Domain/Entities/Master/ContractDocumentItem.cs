using System;
using System.Collections.Generic;

namespace ContractManagement.Domain.Entities.Master;

public partial class ContractDocumentItem
{
    public long ContractDocumentItemId { get; set; }

    public long ContractDocumentId { get; set; }

    public long ContractVersionId { get; set; }

    public long? ProductId { get; set; }

    public string? ContractDocumentItemCode { get; set; }

    public string ContractDocumentItemName { get; set; } = null!;

    public decimal ContractDocumentItemQuantity { get; set; }

    public decimal ContractDocumentItemUnitPrice { get; set; }

    public decimal ContractDocumentItemLineSubtotal { get; set; }

    public decimal ContractDocumentItemDiscountPercent { get; set; }

    public decimal ContractDocumentItemFixedDiscountAmount { get; set; }
}
