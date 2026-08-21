using backend.service.Repository.Implementations;
using backend.service.Repository.Interfaces;
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

            // Domain Services
            services.AddScoped<IDatabaseService, DatabaseService>();
            services.AddScoped<IExportService, ExportService>();

            return services;
        }
    }
}
