using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Mappings.Quotation;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.ContractTemplate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContractManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddContractManagementApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CustomContractPlaceholderOptions>(
            configuration.GetSection(CustomContractPlaceholderOptions.SectionName));

        services.AddSingleton<IContractPaymentDueDateService, ContractPaymentDueDateService>();
        services.AddSingleton<IContractPlaceholderSourceRegistry, ContractPlaceholderSourceRegistry>();
        services.AddSingleton<IContractPlaceholderSourceProvider, ContractPlaceholderSourceProvider>();
        services.AddSingleton<IContractPlaceholderSourceProvider, ContractVersionPlaceholderSourceProvider>();
        services.AddSingleton<IContractPlaceholderSourceProvider, CustomerPlaceholderSourceProvider>();
        services.AddSingleton<IContractPlaceholderSourceProvider, TenantLegalProfilePlaceholderSourceProvider>();
        services.AddSingleton<IContractPlaceholderSourceProvider, ContractOwnerPlaceholderSourceProvider>();
        services.AddSingleton<IContractPlaceholderSourceProvider, DepartmentPlaceholderSourceProvider>();
        services.AddScoped<IContractTemplateDocumentValidator, ContractTemplateDocumentValidator>();
        services.AddScoped<IContractTemplatePreviewRenderer, ContractTemplatePreviewRenderer>();

        services.AddAutoMapper(config =>
        {
            config.AddProfile<QuotationMappingProfile>();
        });

        return services;
    }
}

