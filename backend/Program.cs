using backend.common;
using backend.Extensions;
using backend.Middleware;
using backend.model.DbModels;

namespace backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure AppConfiguration
            builder.Services.Configure<AppConfiguration>(builder.Configuration);
            var appSettings = builder.Configuration.Get<AppConfiguration>() ?? new AppConfiguration();

            // Database setup
            builder.Services.AddDatabase(builder.Configuration, builder.Environment.IsDevelopment());

            // Core Web API & Validation Services
            builder.Services.AddWebApiServices();

            // Domain and Unit of Work Registrations
            builder.Services.AddDomains();
            builder.Services.AddUnitOfWork<AppDbContext>();

            // CORS Policy
            builder.Services.AddCorsPolicy(appSettings);

            var app = builder.Build();

            // Apply migrations on startup
            DatabaseServiceExtension.ApplyMigrations(app);

            // Configure the HTTP request pipeline
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
