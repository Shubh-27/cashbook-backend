using backend.service.Repository.Implementations;
using backend.service.Repository.Interface;
using backend.service.Services.Implementations;
using backend.service.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Extensions
{
    public static class DomainCollectionExtension
    {
        /// <summary>
        /// Registers all repositories and domain services into the DI container.
        /// </summary>
        public static IServiceCollection AddDomains(this IServiceCollection services)
        {
            // Repositories
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IDescriptionRepository, DescriptionRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IImportProfileRepository, ImportProfileRepository>();

            // Domain Services
            services.AddScoped<IDatabaseService, DatabaseService>();
            services.AddScoped<IExportService, ExportService>();
            services.AddScoped<IStatementImportService, StatementImportService>();
            services.AddScoped<IImportProfileService, ImportProfileService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddSingleton<IStatementGridParser, StatementGridParser>();
            services.AddSingleton<IStatementMaskingService, StatementMaskingService>();
            services.AddSingleton<IImportProfilePreviewService, ImportProfilePreviewService>();
            services.AddSingleton<IImportProfileRuleValidator, ImportProfileRuleValidator>();

            return services;
        }
    }
}
