using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EduMy.Backend.Data;
using CloudinaryDotNet;
using Serilog;
using Polly;
using System.Text.Json.Serialization;
using EduMy.Backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/edumy-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IOrderCompletionService, OrderCompletionService>();
builder.Services.AddScoped<ICourseProgressService, CourseProgressService>();
builder.Services.AddScoped<ICourseRatingService, CourseRatingService>();
builder.Services.AddScoped<ICourseWorkflowService, CourseWorkflowService>();
builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ILessonResourceStorage, LessonResourceStorage>();

// Configure Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

// Configure Machine Learning Service with Polly Resilience
builder.Services.AddHttpClient<EduMy.Backend.Services.IMachineLearningService, EduMy.Backend.Services.MLServiceClient>(client =>
{
    var mlUrl = builder.Configuration["MLServiceUrl"] ?? builder.Configuration["MachineLearning:MLServiceUrl"] ?? builder.Configuration["MachineLearning:BaseUrl"] ?? "http://ml-service:8000";
    client.BaseAddress = new Uri(mlUrl);
    client.Timeout = TimeSpan.FromSeconds(8);
})
.AddTransientHttpErrorPolicy(policyBuilder =>
    policyBuilder.WaitAndRetryAsync(2, retryAttempt => TimeSpan.FromMilliseconds(250 * retryAttempt)))
.AddTransientHttpErrorPolicy(policyBuilder =>
    policyBuilder.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];

if (string.IsNullOrWhiteSpace(googleClientId) || googleClientId.Contains("your-google-client-id") || googleClientId == "mock-google-client-id")
{
    Log.Warning("Google Authentication ClientId is missing or using placeholder. Google login functionality will show a configuration error to users.");
    googleClientId = "mock-google-client-id";
}

if (string.IsNullOrWhiteSpace(googleClientSecret) || googleClientSecret.Contains("your-google-client-secret") || googleClientSecret == "mock-google-client-secret")
{
    googleClientSecret = "mock-google-client-secret";
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
})
.AddCookie("ExternalCookie")
.AddGoogle(options =>
{
    options.ClientId = googleClientId;
    options.ClientSecret = googleClientSecret;
    options.SignInScheme = "ExternalCookie";
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.SetIsOriginAllowed(_ => true)
               .AllowCredentials()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Configure Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// The physical static-file provider is created at startup. Ensure its root
// exists before UseStaticFiles so files uploaded later are served correctly.
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"));

Stripe.StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];

app.UseMiddleware<EduMy.Backend.Middlewares.ExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "EduMy API v1"));
}

var contentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
contentTypeProvider.Mappings[".doc"] = "application/msword";
contentTypeProvider.Mappings[".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
contentTypeProvider.Mappings[".xls"] = "application/vnd.ms-excel";
contentTypeProvider.Mappings[".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
contentTypeProvider.Mappings[".ppt"] = "application/vnd.ms-powerpoint";
contentTypeProvider.Mappings[".csv"] = "text/csv";
contentTypeProvider.Mappings[".odt"] = "application/vnd.oasis.opendocument.text";
contentTypeProvider.Mappings[".ods"] = "application/vnd.oasis.opendocument.spreadsheet";
contentTypeProvider.Mappings[".odp"] = "application/vnd.oasis.opendocument.presentation";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider
});
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseMiddleware<EduMy.Backend.Middlewares.ActiveUserMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect) return Results.StatusCode(503);
        var categoriesExist = await db.Categories.AnyAsync();
        return Results.Ok(new { status = "Healthy", database = true, categories = categoriesExist });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// Seed database with retry loop to wait for SQL Server startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    const int maxDbRetries = 12;
    for (int retry = 1; retry <= maxDbRetries; retry++)
    {
        try
        {
            logger.LogInformation("Attempting database migration and seeding (attempt {Attempt}/{MaxAttempts})...", retry, maxDbRetries);
            EduMy.Backend.Data.DataSeeder.Initialize(services);
            logger.LogInformation("Database migration and seeding completed successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database initialization attempt {Attempt} failed.", retry);
            if (retry == maxDbRetries)
            {
                logger.LogError(ex, "Database initialization failed after {MaxAttempts} attempts.", maxDbRetries);
            }
            else
            {
                System.Threading.Thread.Sleep(2000);
            }
        }
    }
}

app.Run();
