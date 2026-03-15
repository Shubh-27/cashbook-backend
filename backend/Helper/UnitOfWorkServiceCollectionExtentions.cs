using backend.service.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace backend.Helper
{
    public static class UnitOfWorkServiceCollectionExtentions
    {
        public static IServiceCollection AddUnitOfWork<TContext>(this IServiceCollection services)
           where TContext : DbContext
        {
            services.AddTransient<IRepositoryFactory, UnitOfWork<TContext>>();
            services.AddTransient<IUnitOfWork, UnitOfWork<TContext>>();
            services.AddTransient<IUnitOfWork<TContext>, UnitOfWork<TContext>>();
            return services;
        }
    }
}
