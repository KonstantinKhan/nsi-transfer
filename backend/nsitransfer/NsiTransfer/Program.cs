using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using NsiTransfer.Authentication;
using NsiTransfer.BLL;
using NsiTransfer.DAL;
using NsiTransfer.Extensions;
using NsiTransfer.Middlewares;
using NsiTransfer.Presentation.BackgroundServices;
using Serilog;
using System.Reflection;


namespace NsiTransfer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.AddLoggingWithSerilog();
            builder.BindConfigModels();


            // Настройка простой аутентификации по Bearer токену
            // Если вдруг Полином API изменит тип аутентификации с Bearer на какую-то другую, то тут всё сломается,
            // потому что аутентификация в NsiTransfer по токену от самого Полинома
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "SimpleBearerAuth";
                options.DefaultChallengeScheme = "SimpleBearerAuth";
            })
            .AddScheme<AuthenticationSchemeOptions, SimpleBearerAuthenticationHandler>("SimpleBearerAuth", options => { });

            // Добавляем авторизацию с пустой политикой (по умолчанию требуется аутентификация)
            builder.Services.AddAuthorization();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(setup =>
            {
                setup.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    Description = "Введите AccessToken по шаблону: `Bearer AccessToken`"
                });

                // Указание, что необходимо добавлять security definition с Id = Bearer во все запросы, которые идут в endpoints с атрибутом [Authorize]
                setup.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                        Array.Empty<string>()
                    }
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                setup.IncludeXmlComments(xmlPath);
            });

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.SetIsOriginAllowed(_ => true) // Разрешить все Origins
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });


            // Presentation
            builder.Services.AddHostedService<PolynomApiSyncBackgroundService>();

            // BLL
            builder.Services.AddBllServices(builder.Configuration);

            // DAL
            builder.Services.AddDalServices(builder.Configuration);

            


            var app = builder.Build();

            app.UseMiddleware<ExceptionHandlingMiddleware>();

            app.Services.MigrateDbContext(app.Environment);

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();

            //app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Lifetime.ApplicationStarted.Register(() =>
            {
                foreach (var url in app.Urls)
                {
                    Log.Information("Приложение слушает на: {url}", url);
                }
            });

            app.Run();
        }
    }
}
