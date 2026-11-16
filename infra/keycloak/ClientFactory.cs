using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

/// <summary>Factory для clients: Authorization Code Flow (Postman, Swagger) і Client Credentials Flow (сервіси).</summary>
public static class ClientFactory
{
    public const string PostmanRedirect = "https://oauth.pstmn.io/v1/callback";

    public static readonly string[] DefaultScopes = { "profile", "email", "roles", "web-origins", "basic", "acr" };

    public static Keycloak.OpenId.Client CreateAuthorizationCodeClient(
        Keycloak.Realm realm, string clientId, string name, string[] redirectUris,
        bool isPublic, bool allowDirectGrant, CustomResourceOptions opts)
        => new($"client-{clientId}", new Keycloak.OpenId.ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            Enabled = true,
            AccessType = isPublic ? "PUBLIC" : "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = allowDirectGrant,   // лише для скриптової перевірки матриці
            ValidRedirectUris = redirectUris,
            WebOrigins = new[] { "+" },
            PkceCodeChallengeMethod = "S256"                // PKCE обов'язковий
        }, opts);

    public static Keycloak.OpenId.Client CreateClientCredentialsClient(
        Keycloak.Realm realm, string clientId, string name, CustomResourceOptions opts)
        => new($"client-{clientId}", new Keycloak.OpenId.ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            ServiceAccountsEnabled = true                   // Client Credentials: токен сервісу без користувача
        }, opts);
}

public static class ClientExtensions
{
    /// <summary>Додає audience (client_id API) у access token клієнта.</summary>
    public static Keycloak.OpenId.Client WithAudience(
        this Keycloak.OpenId.Client client, Keycloak.Realm realm, string logicalName, string audience,
        CustomResourceOptions opts)
    {
        _ = new Keycloak.OpenId.AudienceProtocolMapper($"audience-{logicalName}-{audience}",
            new Keycloak.OpenId.AudienceProtocolMapperArgs
            {
                RealmId = realm.Id,
                ClientId = client.Id,
                Name = $"audience-{audience}",
                IncludedClientAudience = audience,
                AddToAccessToken = true,
                AddToIdToken = false
            }, opts);
        return client;
    }

    /// <summary>Фіксує default client scopes (sub у Keycloak 26 дає scope "basic", тому його не прибираємо).</summary>
    public static Keycloak.OpenId.Client WithDefaultScopes(
        this Keycloak.OpenId.Client client, Keycloak.Realm realm, string logicalName, string[] scopes,
        CustomResourceOptions opts)
    {
        _ = new Keycloak.OpenId.ClientDefaultScopes($"default-scopes-{logicalName}",
            new Keycloak.OpenId.ClientDefaultScopesArgs
            {
                RealmId = realm.Id,
                ClientId = client.Id,
                DefaultScopes = scopes
            }, opts);
        return client;
    }
}