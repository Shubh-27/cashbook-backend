using backend.service.Repository.Implementation;
using backend.service.Repository.Interface;

namespace backend.Helper
{
    public static class DomainCollectionExtension
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDomains(this IServiceCollection services)
        {
            // Example services.AddScoped<IRepository, Repository>();
            services.AddScoped<IAccountRepository, AccountRepository>();
            services.AddScoped<IDescriptionRepository, DescriptionRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();
            services.AddScoped<IExportRepository, ExportRepository>();
            return services;
        }
    }
}
