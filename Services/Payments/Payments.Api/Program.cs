using Microsoft.AspNetCore.Authorization;
using Platform.ServiceDefaults;
using Platform.ServiceDefaults.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// “ри способи автентиф≥кац≥њ; схему обираЇ пол≥тика-схема за issuer токена
builder.Services.AddAuthentication(o =>
{
    o.DefaultScheme = AuthSchemes.Multi;
    o.DefaultChallengeScheme = AuthSchemes.Multi;
})
.AddPolicyScheme(AuthSchemes.Multi, "Own JWT, IdentityServer or Keycloak",
    o => o.ForwardDefaultSelector = ctx => AuthSchemes.SelectByIssuer(ctx, builder.Configuration, keycloakEnabled: true))
.AddJwtBearer(AuthSchemes.OwnJwt,
    o => o.TokenValidationParameters = AuthSchemes.OwnJwtParameters(builder.Configuration))
.AddJwtBearer(AuthSchemes.IdentityServer, o =>
{
    o.Authority = builder.Configuration["IdentityServer:Authority"];
    o.Audience = "payments_api";
    o.RequireHttpsMetadata = false;
})
.AddKeycloakJwtBearer("keycloak", "DevRealmDrivers", AuthSchemes.Keycloak, options =>
{
    options.Audience = "payments-api";                  // resource server у Keycloak
    if (builder.Environment.IsDevelopment())
        options.RequireHttpsMetadata = false;           // Keycloak у розробц≥: HTTP
});

// Permission-based авторизац≥€ (таблиц€ 3.3.3): пол≥тика на кожен permission
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in new[] { "payments:read", "payments:process", "payments:refund" })
        options.AddPolicy(permission, p => p.Requirements.Add(new PermissionRequirement(permission)));
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseCorrelationId();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();