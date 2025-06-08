using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;

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
            var section = Configuration.GetSection("AppSettings");
            var settings = section.Get<Settings>();
            services.Configure<Settings>(section);
            services.AddHttpContextAccessor();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", builder =>
                {
                    builder
                        .WithOrigins(
                            "http://localhost:5173",
                            "http://localhost:5174",
                            "https://localhost:5173",
                            "https://localhost:5174"
                        )
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });

                options.AddPolicy("AllowAll", builder =>
                {
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });

            services.AddDbContext<DataContext>(options =>
            {
                options
                    .UseSqlServer(settings.SqldbConnString);
            });

            services.AddControllers(options =>
            {
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new EnumStringConverter());
            });

            services.AddSwaggerGen(s =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                s.IncludeXmlComments(xmlPath);

                s.CustomOperationIds(e =>
                {
                    return e.TryGetMethodInfo(out MethodInfo methodInfo) ? methodInfo.Name : null;
                });

                s.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme. \r\n\r\n 
                                    Enter 'Bearer' [space] and then your token in the text input below.
                                    \r\n\r\nExample: 'Bearer 12345abcdef'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                s.AddSecurityRequirement(new OpenApiSecurityRequirement()
                  {
                    {
                      new OpenApiSecurityScheme
                      {
                        Reference = new OpenApiReference
                          {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                          },
                          Scheme = "oauth2",
                          Name = "Bearer",
                          In = ParameterLocation.Header,
                        },
                        new List<string>()
                      }
                    });
            });

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

            services.AddScoped<AuthContext>();
            services.AddScoped<Repository>();
            services.AddSingleton(settings);
            services.AddHttpContextAccessor();
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
            
            // app.UseCorsMiddleware(); 

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}