using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

namespace Core
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services

            // Database
            builder.Services.AddDbContext<DatabaseContext>(opt =>
            {
                opt.UseInMemoryDatabase("JustAnotherList");
            });
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            // Auth
            builder.Services.AddAuthentication("Bearer");
            builder.Services.AddAuthorization();
            builder.Services.AddIdentityApiEndpoints<IdentityUser>()
                .AddEntityFrameworkStores<DatabaseContext>();

            // API Documentation
            builder.Services.AddOpenApi();

            // Configure services

            // Auth
            builder.Services.Configure<IdentityOptions>(opt =>
            {
                opt.Password.RequireDigit = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequireNonAlphanumeric = true;
                opt.Password.RequireUppercase = true;
                opt.Password.RequiredLength = 6;
                opt.Password.RequiredUniqueChars = 1;

                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(3);
                opt.Lockout.MaxFailedAccessAttempts = 7;
                opt.Lockout.AllowedForNewUsers = true;

                opt.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                opt.User.RequireUniqueEmail = false;
            });
            builder.Services.ConfigureApplicationCookie(opt =>
            {
                // Cookie settings
                opt.Cookie.HttpOnly = true;
                opt.ExpireTimeSpan = TimeSpan.FromMinutes(5);

                opt.LoginPath = "/auth/login";
                opt.AccessDeniedPath = "/auth/denied";
                opt.SlidingExpiration = true;
            });

            var app = builder.Build();

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapIdentityApi<IdentityUser>();
            app.MapChecklistApi();

            app.MapOpenApi();
            app.MapScalarApiReference(opt =>
            {
                opt
                    .WithPreferredScheme("Bearer")
                    .WithHttpBearerAuthentication(bearer =>
                    {
                        bearer.Token = "your-bearer-token";
                    });
            });

            app.Run();
        }
    }
}
