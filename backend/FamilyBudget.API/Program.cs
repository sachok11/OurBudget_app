using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;
using Microsoft.Extensions.Options;

using FamilyBudget.Infrastructure.Data;
using FamilyBudget.Application.Services;
using FamilyBudget.Infrastructure.Services;
using FamilyBudget.API.Hubs;
using FamilyBudget.Core.Configuration;
using FamilyBudget.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ===============================
// DATABASE (PostgreSQL)
// ===============================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// ===============================
// APPLICATION SERVICES
// ===============================
builder.Services.AddScoped<IAuthService, AuthService>();

// ===============================
// SIGNALR
// ===============================
builder.Services.AddSignalR();

// ===============================
// JWT AUTHENTICATION
// ===============================
var jwtSecret =
    builder.Configuration["Jwt:Secret"]
    ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.ASCII.GetBytes(jwtSecret)
                ),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
    });

// ===============================
// REDIS CONFIGURATION
// ===============================
builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection("Redis")
);

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var settings =
        sp.GetRequiredService<IOptions<RedisSettings>>().Value;

    var configuration =
        ConfigurationOptions.Parse(settings.ConnectionString);

    configuration.AbortOnConnectFail = settings.AbortOnConnectFail;
    configuration.ConnectTimeout = settings.ConnectTimeout;
    configuration.KeepAlive = 60;
    configuration.ConnectRetry = 3;
    configuration.ClientName = "FamilyBudgetApp";

    var logger = sp.GetRequiredService<ILogger<Program>>();

    try
    {
        var connection =
            ConnectionMultiplexer.Connect(configuration);

        logger.LogInformation(
            "Connected to Redis: {Connection}",
            settings.ConnectionString
        );

        connection.ConnectionFailed += (sender, args) =>
        {
            logger.LogError(
                "Redis connection failed: {FailureType}",
                args.FailureType
            );
        };

        connection.ConnectionRestored += (sender, args) =>
        {
            logger.LogInformation(
                "Redis connection restored: {FailureType}",
                args.FailureType
            );
        };

        return connection;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Redis connection failed");
        throw;
    }
});

builder.Services.AddScoped<ICacheService, RedisCacheService>();

// ===============================
// API + SWAGGER
// ===============================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IMessageQueueService, RabbitMQService>();
builder.Services.AddHostedService<MessageProcessorService>();


// ===============================
// BUILD APP
// ===============================
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ===============================
// MIDDLEWARE
// ===============================
app.UseAuthentication();
app.UseAuthorization();

// ===============================
// SIGNALR HUB
// ===============================
app.MapHub<FamilyHub>("/hubs/family");

// ===============================
// CONTROLLERS
// ===============================
app.MapControllers();

app.Run();
