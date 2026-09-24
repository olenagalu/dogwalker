using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PawsAndPaths.Api.Data;
using PawsAndPaths.Api.Models;
using PawsAndPaths.Api.Services;

var builder = WebApplication.CreateBuilder(args);
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var publicPort))
    builder.WebHost.UseUrls($"http://0.0.0.0:{publicPort}");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString) && !string.IsNullOrWhiteSpace(builder.Configuration["DB_HOST"]))
{
    connectionString = new NpgsqlConnectionStringBuilder
    {
        Host = builder.Configuration["DB_HOST"],
        Port = int.TryParse(builder.Configuration["DB_PORT"], out var port) ? port : 5432,
        Database = builder.Configuration["DB_NAME"],
        Username = builder.Configuration["DB_USER"],
        Password = builder.Configuration["DB_PASSWORD"],
        SslMode = SslMode.Require
    }.ConnectionString;
}
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Database connection is not configured.");
}
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentityCore<AppUser>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PrincessDogWalker",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PrincessDogWalker.Web",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("account", limiter =>
    {
        limiter.PermitLimit = 12;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
    options.AddFixedWindowLimiter("contact", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(5);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IWelcomeEmailSender, WelcomeEmailSender>();
builder.Services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();
builder.Services.AddScoped<IAccountDecisionEmailSender, AccountDecisionEmailSender>();
builder.Services.AddScoped<IBookingDecisionEmailSender, BookingDecisionEmailSender>();
builder.Services.AddScoped<IOwnerNotificationEmailSender, OwnerNotificationEmailSender>();
builder.Services.AddScoped<ICustomerManagementService, CustomerManagementService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}
using (var scope = app.Services.CreateScope())
    await DatabaseSeeder.SeedIdentityAsync(scope.ServiceProvider, builder.Configuration);

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(exception, "Unhandled request error");
    await Results.Problem(title: "The request could not be completed.",
        statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
}));
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://accounts.google.com; script-src 'self' https://accounts.google.com/gsi/client; frame-src https://accounts.google.com/gsi/ https://www.openstreetmap.org; connect-src 'self' https://accounts.google.com/gsi/; font-src 'self' data: https://fonts.gstatic.com; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});
app.UseRouting();
app.UseRateLimiter();
app.UseCors("Frontend");
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var extension = Path.GetExtension(context.File.Name);
        if (extension is ".html" or ".css" or ".js")
        {
            context.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Context.Response.Headers.Pragma = "no-cache";
            context.Context.Response.Headers.Expires = "0";
        }
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/gallery.html", () => Results.NotFound());
app.MapGet("/gallery", () => Results.NotFound());
app.MapGet("/about.html", () => Results.NotFound());
app.MapGet("/about", () => Results.NotFound());
app.Map("/api/{**path}", () => Results.NotFound(new { message = "API endpoint not found." }));
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
