using Gibs.Application.Services;
using Gibs.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Infrastructure;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using GibsLifesMicroWebApi.Others;
using GibsLifesMicroWebApi.Services;
using GibsLifesMicroWebApi.Services.GroupLife;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static GibsLifesMicroWebApi.Data.Repositories.Repository;

namespace GibsLifesMicroWebApi
{
    public class EnumStringConverter : JsonConverter<object>
    {
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var stringValue = reader.GetString();

            Type enumType = typeToConvert.IsEnum ?
                typeToConvert : Nullable.GetUnderlyingType(typeToConvert);

            if (string.IsNullOrEmpty(stringValue))
            {
                if (typeToConvert.IsEnum)
                    throw NewEnumJsonException(enumType, "Missing value.");
                else
                    return null;
            }

            if (Enum.TryParse(enumType, stringValue, true, out var enumValue))
                return enumValue;
            else
                throw NewEnumJsonException(enumType, "Invalid entry.");

            static JsonException NewEnumJsonException(Type enumType, string message)
            {
                string[] values = Enum.GetNames(enumType);
                return new JsonException($"{message} Use the following: {string.Join(", ", values)}");
            }
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            var enumString = value.ToString();
            writer.WriteStringValue(enumString);
        }

        public override bool CanConvert(Type typeToConvert)
        {
            if (typeToConvert.IsEnum)
                return true;

            if (typeToConvert.IsNullableEnum())
                return true;

            return base.CanConvert(typeToConvert);
        }
    }

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // ===== App Settings =====
            var section = Configuration.GetSection("AppSettings");
            var settings = section.Get<Settings>();
            services.Configure<Settings>(section);
            services.AddSingleton(settings);

            services.AddHttpContextAccessor();

            // ===== CORS =====
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", builder =>
                {
                    builder.WithOrigins(
                        "http://localhost:5173",
                        "https://localhost:5173",
                        "http://localhost:5174",
                        "http://localhost:3000",
                        "https://localhost:3000",
                        "http://microlife.newgibsonline.com",
                        "https://microlife.newgibsonline.com",
                        "http://lifetechnical.gibsapponline.com",
                        "https://lifetechnical.gibsapponline.com",
                        "https://ucrlife.newgibsonline.com",
                        "https://fortislife.newgibsonline.com",
                        "http://portalife.newgibsonline.com",
                        "https://portalife.newgibsonline.com"
                    // Add other origins if needed
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
                });

                options.AddPolicy("AllowAll", builder =>
                {
                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });

            // ===== Controllers + JSON Options =====
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.Converters.Add(new EnumStringConverter());
                });

            // ===== Add DbContext =====
            services.AddDbContext<DataContext>(options =>
            {
                options.UseSqlServer(settings.SqldbConnString, sqlOptions =>
                {
                    sqlOptions.CommandTimeout(60);
                    sqlOptions.EnableRetryOnFailure();
                });
            });

            // ===== Register Repository =====
            services.AddScoped<Repository>();

            // ===== Register PolicyService (with DbContext injected automatically) =====
            //services.AddScoped<PolicyService>();
            services.AddScoped<AutonumberService>();
            services.AddScoped<InsuredRepository>();
            services.AddScoped<AutoNumberPrepo>();
            services.AddScoped<Infrastructure.Repositories.PremiumCalculator>();
            services.AddScoped<AutoNumberService>();
            services.AddScoped<BulkMemberExcelParser>();
            services.AddScoped<LookupService>();
            services.AddScoped<PolicyService>();
            services.AddScoped<GroupLifeDetailService>();
            services.AddScoped<AutoNumberRepository>();
            services.AddScoped<GroupLifeMemberService>();
            services.AddScoped<SubRiskSectionService>();
            services.AddScoped<PolicyDocumentService>();
            services.AddScoped<SubRiskSMIService>();
            services.AddScoped<IGroupLifeCoinsuranceNoteService, GroupLifeCoinsuranceNoteService>();
            services.AddScoped<ClaimReserveService>();
            services.AddScoped<ProductSettingService>();
            services.AddScoped<RiskService>();
            services.AddSingleton<AutoNumberSettings>();
            // ===== Other Repositories & Services =====
            services.AddScoped<BeneficiaryRepository>();
            services.AddScoped<ProposalService>();
            services.AddScoped<AutoNumberServices>();
            services.AddScoped<BulkUploadErrorService>();
            services.AddScoped<ProposalReportService>();
            services.AddHttpClient<PaystackService>();
            services.AddScoped<PaystackService>();
            services.AddScoped<AuthContext>();
            services.AddScoped<GroupLifeDebitNoteService>();

            // ===== Authentication (JWT) =====
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.SaveToken = true;
                x.RequireHttpsMetadata = false;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(settings.JwtSecret))
                };
            });

            // ===== Authorization =====
            services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
            services.AddAuthorization();

            // ===== Swagger =====
            services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);

                c.CustomOperationIds(e => e.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null);

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme. 
                                    Enter 'Bearer' [space] and then your token.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Uploads")),
                RequestPath = "/uploads"
            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "GibsLifesMicroWebApi V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseRouting();

            app.UseCors("AllowSpecificOrigins");

            app.UseAuthentication();
            app.UseAuthorization();

            // Run startup tasks (permissions, caches, etc.)
            RunStartupTasks(app.ApplicationServices).GetAwaiter().GetResult();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private static async Task RunStartupTasks(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var policyProvider = scope.ServiceProvider.GetService<IAuthorizationPolicyProvider>() as PermissionPolicyProvider;
            var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

            // Register all permissions dynamically as policies
            //var permissions = await db.Permissions.AsNoTracking().ToListAsync();

            //foreach (var permission in permissions)
            //{
            //    if (!authOptions.GetPolicy(permission.PermissionName)?.Requirements.Any() ?? true)
            //    {
            //        authOptions.AddPolicy(permission.PermissionName, policy =>
            //            policy.Requirements.Add(new PermissionRequirement(permission.PermissionName)));
            //    }
            //}
        }
    }
}