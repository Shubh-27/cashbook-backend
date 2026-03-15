
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

            // Determine DB path dynamically.
            // In production (packaged), store in user's local AppData.
            // In development, fall back to the relative path in appsettings.json.
            string dbConnectionString;
            if (!builder.Environment.IsDevelopment())
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dbFolder = Path.Combine(appDataPath, "BankApp");
                Directory.CreateDirectory(dbFolder);
                var dbFilePath = Path.Combine(dbFolder, "bank_transaction_db.db");
                dbConnectionString = $"Data Source={dbFilePath};Cache=Shared;Pooling=True;";
            }
            else
            {
                dbConnectionString = builder.Configuration["DefaultConnection"] ?? string.Empty;
            }

            #region ||DBContext configuration||

            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(dbConnectionString);
                options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
                options.EnableSensitiveDataLogging(true);

            }, ServiceLifetime.Scoped);

            #endregion

            builder.Services.AddTransient<IValidatorInterceptor, FluentInterceptor>();

            builder.Services.AddControllers();
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

            // Apply EF Core migrations on startup (creates or upgrades DB schema automatically)
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // No HTTPS redirect — this is a local desktop app
            app.UseCors("AllowAll");
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
