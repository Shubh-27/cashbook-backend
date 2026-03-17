
using backend.Helper;
using backend.model.Data;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;

namespace backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            DatabaseServiceExtension.AddDatabase(builder);

            builder.Services.AddControllers()
                .AddFluentValidation(fv => {
                    fv.RegisterValidatorsFromAssemblyContaining<backend.Validators.AccountRequestValidator>();
                });

            builder.Services.AddTransient<IValidatorInterceptor, FluentInterceptor>();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            #region ||Repository Dependancy||
            DomainCollectionExtension.AddDomains(builder.Services);
            UnitOfWorkServiceCollectionExtentions.AddUnitOfWork<AppDbContext>(builder.Services);
            #endregion

            // CORS: Allow any origin for Electron/local dev usage (no credentials needed)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            DatabaseServiceExtension.ApplyMigrations(app);

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // No HTTPS redirect — this is a local desktop app
            app.UseHttpStatusCodeExceptionMiddleware();
            app.UseCors("AllowAll");
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
