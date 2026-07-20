using Bitai.LDAPGateway.Application;
using Bitai.LDAPGateway.Infrastructure;
using Bitai.LDAPGateway.Api.Middleware;
using Bitai.LDAPGateway.Api.Options;

var builder = WebApplication.CreateBuilder(args);

const string WebApiCorsPolicyName = "WebApiCorsPolicy";
var webApiCorsConfiguration =
    builder.Configuration.GetSection(WebApiCorsConfiguration.SectionName).Get<WebApiCorsConfiguration>()
    ?? new WebApiCorsConfiguration();

builder.Services
    .AddOptions<WebApiCorsConfiguration>()
    .BindConfiguration(WebApiCorsConfiguration.SectionName)
    .Validate(configuration => configuration.AllowAnyOrigin || configuration.AllowedOrigins.Count > 0,
        "At least one allowed origin must be configured when AllowAnyOrigin is false.")
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebApiCorsPolicyName, policyBuilder =>
    {
        if (webApiCorsConfiguration.AllowAnyOrigin)
        {
            policyBuilder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policyBuilder
            .WithOrigins(webApiCorsConfiguration.AllowedOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApiDocument(config => {
    config.DocumentName = "v1";
    config.Title = "BITAI LDAP Gateway API";
    config.Version = "v1";
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
	app.UseOpenApi();
	app.UseSwaggerUi();
}

app.UseHttpsRedirection();
app.UseCors(WebApiCorsPolicyName);
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
