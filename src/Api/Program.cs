using Bitai.LDAPGateway.Application;
using Bitai.LDAPGateway.Infrastructure;
using Bitai.LDAPGateway.Api.Middleware;
using Bitai.LDAPGateway.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.ConfigureRouteConstraints();
builder.Services.AddControllers();
builder.Services.AddCorsConfiguration(builder.Configuration);
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
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
