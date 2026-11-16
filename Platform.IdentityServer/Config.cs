using Duende.IdentityServer.Models;

namespace Platform.IdentityServer;

public static class Config
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile()
    ];

    // Гранульовані дозволи: окремий scope на кожну дію, а не «все або нічого»
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new("orders.read", "Read orders"),
        new("orders.write", "Create and change orders"),
        new("products.read", "Read products"),
        new("products.write", "Create and change products"),
        new("reviews.read", "Read reviews"),
        new("reviews.write", "Create and change reviews"),
        new("aggregator.read", "Read aggregated orders")
    ];

    // ApiResource = мікросервіс; його ім'я потрапляє в claim aud, а сервіс перевіряє Audience
    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("orders_api", "Orders API") { Scopes = { "orders.read", "orders.write" } },
        new("products_api", "Catalog API") { Scopes = { "products.read", "products.write" } },
        new("reviews_api", "Reviews API") { Scopes = { "reviews.read", "reviews.write" } },
        new("aggregator_api", "Aggregator API") { Scopes = { "aggregator.read" } }
    ];

    public static IEnumerable<Client> Clients =>
    [
        // Postman: Authorization Code (користувач входить через браузер)
        new Client
        {
            ClientId = "postman",
            AllowedGrantTypes = GrantTypes.Code,
            ClientSecrets = { new Secret("secret".Sha256()) },
            RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
            AllowedScopes =
            {
                "openid", "profile",
                "orders.read", "orders.write", "products.read", "products.write",
                "reviews.read", "reviews.write", "aggregator.read"
            },
            AllowOfflineAccess = true
        },

        // Aggregator: Client Credentials (сервіс-до-сервісу, без користувача)
        new Client
        {
            ClientId = "aggregator_service",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("aggregator_secret".Sha256()) },
            AllowedScopes = { "orders.read", "products.read", "reviews.read" }
        },

        // Swagger UI: public client з PKCE, без секрету
        new Client
        {
            ClientId = "swagger",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:5044/swagger/oauth2-redirect.html" },
            AllowedCorsOrigins = { "http://localhost:5044" },   // обмін code на токен робить браузер
            AllowedScopes = { "openid", "profile", "orders.read", "orders.write" }
        }
    ];
}