using Core;
using Core.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Service Registrations ---

// Network
builder.Services.AddCors();

// Database
builder.AddSqlServerClient(connectionName: "database");
builder.Services.AddDbContext<DatabaseContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("database"));
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Authentication & Authorization
builder.Services.AddAuthentication()
    .AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<DatabaseContext>();

// API Documentation
builder.Services.AddOpenApi(opt =>
    opt.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

// Identity Options
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

// Cookie Settings
builder.Services.ConfigureApplicationCookie(opt =>
{
    opt.Cookie.HttpOnly = true;
    opt.ExpireTimeSpan = TimeSpan.FromDays(30);
    opt.LoginPath = "/auth/login";
    opt.AccessDeniedPath = "/auth/denied";
    opt.SlidingExpiration = true;
});

var app = builder.Build();

// --- Middleware Pipeline ---

app.UseCors(policyBuilder => policyBuilder
    .AllowAnyHeader()
    .AllowAnyMethod()
    .SetIsOriginAllowed(origin => true)
    .AllowCredentials());
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<IdentityUser>();
app.MapChecklistApi();

app.MapOpenApi();
app.MapScalarApiReference(opt =>
{
    opt.AddPreferredSecuritySchemes("Bearer")
        .WithDownloadButton(true)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.Curl);
});

// --- Database Initialization ---

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    await db.Database.MigrateAsync();
}

await app.RunAsync();
