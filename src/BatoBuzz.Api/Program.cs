using BatoBuzz.Api.Infrastructure;
using BatoBuzz.Api.Security;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Application.Services;
using BatoBuzz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace BatoBuzz.Api;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(
                "logs/batobuzz-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 20 * 1024 * 1024,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        try
        {
            Log.Information("Starting BatoBuzz Accounting API");
            var builder = WebApplication.CreateBuilder(args);
            var productVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

            builder.Host.UseSerilog();
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 2 * 1024 * 1024;
                options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
            });

            var signingKeyConfigurationPath = $"{ApiAuthenticationOptions.SectionName}:SigningKey";
            if (string.IsNullOrWhiteSpace(builder.Configuration[signingKeyConfigurationPath])
                && builder.Environment.IsDevelopment())
            {
                builder.Configuration[signingKeyConfigurationPath] =
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
                Log.Warning("No API signing key was configured. A temporary development-only key was generated.");
            }

            builder.Services.AddOptions<ApiAuthenticationOptions>()
                .Bind(builder.Configuration.GetSection(ApiAuthenticationOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Issuer)
                        && !string.IsNullOrWhiteSpace(options.Audience),
                    "Authentication issuer and audience are required.")
                .Validate(
                    options => Encoding.UTF8.GetByteCount(options.SigningKey ?? string.Empty) >= 32,
                    "Authentication:SigningKey must contain at least 32 bytes. Configure it through a secret environment variable in production.")
                .Validate(
                    options => options.AccessTokenMinutes is >= 5 and <= 1440,
                    "Authentication:AccessTokenMinutes must be between 5 and 1440.")
                .ValidateOnStart();

            builder.Services.AddSingleton<AccessTokenService>();
            builder.Services.AddSingleton<ITokenService>(services =>
                services.GetRequiredService<AccessTokenService>());
            builder.Services.AddSingleton<IAccessTokenValidator>(services =>
                services.GetRequiredService<AccessTokenService>());
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SignedBearerAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = SignedBearerAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, SignedBearerAuthenticationHandler>(
                    SignedBearerAuthenticationHandler.SchemeName,
                    _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(SignedBearerAuthenticationHandler.SchemeName)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            builder.Services.AddProblemDetails();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    httpContext =>
                    {
                        var partitionKey = httpContext.User.FindFirst("sub")?.Value
                            ?? httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown";
                        return RateLimitPartition.GetFixedWindowLimiter(
                            $"request:{partitionKey}",
                            _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = 300,
                                Window = TimeSpan.FromMinutes(1),
                                QueueLimit = 0,
                                AutoReplenishment = true
                            });
                    });
                options.AddPolicy("authentication", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true
                        }));
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        new
                        {
                            Status = StatusCodes.Status429TooManyRequests,
                            Title = "Too many requests",
                            Detail = "Wait before trying again. Repeated authentication attempts are more strictly limited."
                        },
                        cancellationToken);
                };
            });
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new() { Title = "BatoBuzz Accounting API", Version = productVersion });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "BatoBuzz-HMAC",
                    In = ParameterLocation.Header,
                    Description = "Paste the access token returned by api/auth/login."
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = Array.Empty<string>()
                });
            });

            var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
            builder.Services.AddDbContext<BatoBuzzDbContext>(options =>
            {
                if (string.Equals(databaseProvider, "Postgres", StringComparison.OrdinalIgnoreCase))
                {
                    var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres")
                        ?? throw new InvalidOperationException(
                            "DatabaseProvider is set to 'Postgres' but no ConnectionStrings:Postgres value was configured.");
                    options.UseNpgsql(postgresConnectionString);
                }
                else if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                        ?? "Data Source=BatoBuzz.db;Cache=Shared";
                    options.UseSqlite(sqliteConnectionString);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported DatabaseProvider '{databaseProvider}'. Use 'Sqlite' or 'Postgres'.");
                }
            });

            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<ICompanyAccessAuthorizer, CompanyAccessAuthorizer>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<ICompanyService, CompanyService>();
            builder.Services.AddScoped<IAccountingService, AccountingService>();
            builder.Services.AddScoped<ISalesService, SalesService>();
            builder.Services.AddScoped<IPurchaseService, PurchaseService>();
            builder.Services.AddScoped<IInventoryService, InventoryService>();
            builder.Services.AddScoped<IDashboardService, DashboardService>();
            builder.Services.AddScoped<ITdsService, TdsService>();

            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("ConfiguredOrigins", policy =>
                {
                    if (allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins)
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    }
                });
            });

            var trustForwardedHeaders =
                builder.Configuration.GetValue<bool>("ReverseProxy:TrustForwardedHeaders");
            if (trustForwardedHeaders)
            {
                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.ForwardLimit = 1;
                    options.KnownIPNetworks.Clear();
                    options.KnownProxies.Clear();
                });
            }

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BatoBuzzDbContext>();
                db.Database.EnsureCreated();

                if (db.Database.IsSqlite())
                {
                    var connectionString = db.Database.GetConnectionString();
                    if (!string.IsNullOrEmpty(connectionString))
                        SchemaUpgrader.ApplyAll(connectionString);
                }
                else
                {
                    PostgresSchemaUpgrader.ApplyAllAsync(db).GetAwaiter().GetResult();
                }

                if (!HasExpectedSchemaAsync(db, CancellationToken.None).GetAwaiter().GetResult())
                {
                    throw new InvalidOperationException(
                        "The database schema is incomplete after startup upgrades.");
                }
            }

            if (trustForwardedHeaders)
                app.UseForwardedHeaders();

            app.UseExceptionHandler();
            app.UseSerilogRequestLogging();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            if (builder.Configuration.GetValue(
                    "HttpsRedirection:Enabled",
                    !app.Environment.IsDevelopment()))
            {
                app.UseHttpsRedirection();
            }

            app.UseCors("ConfiguredOrigins");
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();
            app.MapControllers();

            app.MapGet("/health/live", () => Results.Ok(new
            {
                Status = "Alive",
                Timestamp = DateTime.UtcNow,
                Version = productVersion
            })).AllowAnonymous();

            app.MapGet("/health", async (BatoBuzzDbContext db, CancellationToken cancellationToken) =>
            {
                try
                {
                    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                    if (canConnect && await HasExpectedSchemaAsync(db, cancellationToken))
                    {
                        return (IResult)Results.Ok(new
                        {
                            Status = "Ready",
                            Database = "ConnectedAndValidated",
                            Timestamp = DateTime.UtcNow,
                            Version = productVersion
                        });
                    }

                    return Results.Json(
                        new
                        {
                            Status = "Unhealthy",
                            Database = canConnect ? "SchemaIncomplete" : "Unavailable",
                            Timestamp = DateTime.UtcNow,
                            Version = productVersion
                        },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
                }
                catch (Exception exception)
                {
                    Log.Warning(exception, "API readiness check could not connect to the database");
                }

                return Results.Json(
                    new
                    {
                        Status = "Unhealthy",
                        Database = "Unavailable",
                        Timestamp = DateTime.UtcNow,
                        Version = productVersion
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }).AllowAnonymous();

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "API terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    private static async Task<bool> HasExpectedSchemaAsync(
        BatoBuzzDbContext db,
        CancellationToken cancellationToken)
    {
        var tableNames = db.Model.GetEntityTypes()
            .Select(entityType => entityType.GetTableName())
            .Where(tableName => !string.IsNullOrWhiteSpace(tableName))
            .Select(tableName => tableName!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tableName => tableName, StringComparer.Ordinal)
            .ToArray();
        if (tableNames.Length == 0)
            return false;

        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        try
        {
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            var parameterNames = new List<string>(tableNames.Length);
            for (var index = 0; index < tableNames.Length; index++)
            {
                var parameterName = $"p{index}";
                parameterNames.Add($"@{parameterName}");
                var parameter = command.CreateParameter();
                parameter.ParameterName = parameterName;
                parameter.Value = tableNames[index];
                command.Parameters.Add(parameter);
            }

            var provider = db.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText =
                    $"SELECT COUNT(DISTINCT name) FROM sqlite_master WHERE type='table' AND name IN ({string.Join(", ", parameterNames)})";
            }
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                command.CommandText =
                    $"SELECT COUNT(DISTINCT table_name) FROM information_schema.tables WHERE table_schema=current_schema() AND table_name IN ({string.Join(", ", parameterNames)})";
            }
            else
            {
                return false;
            }

            var presentTableCount = Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            return presentTableCount == tableNames.Length;
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }
}
