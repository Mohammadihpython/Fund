using Api.Endpoints;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// The connection string is read from configuration, which is backed by user-secrets locally
// and environment variables when deployed. It is deliberately NOT in appsettings.json — see the
// repo-hygiene rule in AGENTS.md. Failing here at startup beats failing on the first query.
var connectionString = builder.Configuration.GetConnectionString("FallahFund")
    ?? throw new InvalidOperationException(
        "Connection string 'FallahFund' is not configured. Set it via " +
        "`dotnet user-secrets --project Api set \"ConnectionStrings:FallahFund\" \"<value>\"`.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApplication();

// No real SMTP provider exists yet, so development logs the verification token instead of
// sending it. Guarded to Development inside the extension method — registering this in
// production would put verification tokens in the log.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDevelopmentEmailSender();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Order matters: authentication must run before authorization, and both before the endpoints
// that inspect User.
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();

/// <summary>
/// Named so integration tests can reference the entry-point assembly with
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;