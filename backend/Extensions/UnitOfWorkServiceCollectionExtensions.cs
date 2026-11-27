using backend.service.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace backend.Extensions
{
    public static class UnitOfWorkServiceCollectionExtensions
    {
        public static IServiceCollection AddUnitOfWork<TContext>(this IServiceCollection services)
           where TContext : DbContext
        {
            services.AddScoped<UnitOfWork<TContext>>();
            services.AddScoped<IRepositoryFactory>(sp => sp.GetRequiredService<UnitOfWork<TContext>>());
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork<TContext>>());
            services.AddScoped<IUnitOfWork<TContext>>(sp => sp.GetRequiredService<UnitOfWork<TContext>>());
            return services;
        }
    }
}
