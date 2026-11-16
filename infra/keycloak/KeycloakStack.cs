using System.Collections.Immutable;
using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

public class KeycloakStack : Stack
{
    public const string RealmName = "DevRealmDrivers";

    [Output("RealmUrl")] public Output<string> RealmUrl { get; set; } = null!;
    [Output("AdminConsoleUrl")] public Output<string> AdminConsoleUrl { get; set; } = null!;
    [Output("ClientIds")] public Output<ImmutableArray<string>> ClientIds { get; set; } = null!;
    [Output("ClientSecrets")] public Output<ImmutableDictionary<string, string>> ClientSecrets { get; set; } = null!;

    public KeycloakStack()
    {
        var config = new Pulumi.Config();
        var keycloakUrl = config.Get("keycloakUrl") ?? "http://localhost:8080";
        var temporaryPasswords = config.GetBoolean("temporaryPasswords") ?? true;

        var provider = new Keycloak.Provider("keycloak", new Keycloak.ProviderArgs
        {
            Url = keycloakUrl,
            ClientId = "admin-cli",
            Username = config.Get("adminUsername") ?? "admin",
            Password = config.GetSecret("adminPassword") ?? Output.CreateSecret("admin123"),
            InitialLogin = false
        });
        var opts = new CustomResourceOptions { Provider = provider };

        // 3.2: realm
        var realm = new Keycloak.Realm("dev-realm", new Keycloak.RealmArgs
        {
            RealmName = RealmName,
            Enabled = true,
            DisplayName = "Dev Realm (Drivers)",
            RegistrationAllowed = true,
            ResetPasswordAllowed = true,
            RememberMe = true,
            LoginTheme = "keycloak"
        }, opts);

        // 3.3: ролі та resource servers
        var roles = ResourceServers.CreateRoles(realm, opts);
        ResourceServers.CreateAll(realm, roles, opts);

        // 3.4: clients через factory + extension methods
        var postman = ClientFactory
            .CreateAuthorizationCodeClient(realm, "orders.postman", "Orders (Postman)",
                new[] { ClientFactory.PostmanRedirect }, isPublic: false, allowDirectGrant: true, opts)
            .WithDefaultScopes(realm, "orders-postman", ClientFactory.DefaultScopes, opts);

        var swagger = ClientFactory
            .CreateAuthorizationCodeClient(realm, "orders.swagger", "Orders (Swagger UI)",
                new[] { "http://localhost:5044/swagger/oauth2-redirect.html" }, isPublic: true, allowDirectGrant: false, opts)
            .WithDefaultScopes(realm, "orders-swagger", ClientFactory.DefaultScopes, opts);

        var aggregator = ClientFactory
            .CreateClientCredentialsClient(realm, "aggregator", "Aggregator (service)", opts)
            .WithAudience(realm, "aggregator", "orders-api", opts)
            .WithAudience(realm, "aggregator", "products-api", opts);

        TestUsers.Create(realm, roles, temporaryPasswords, opts);

        // 3.6: outputs
        RealmUrl = Output.Format($"{keycloakUrl}/realms/{realm.RealmName}");
        AdminConsoleUrl = Output.Create($"{keycloakUrl}/admin/master/console/#/{RealmName}");
        ClientIds = Output.All(postman.ClientId, swagger.ClientId, aggregator.ClientId);
        ClientSecrets = Output.Tuple(postman.ClientSecret, aggregator.ClientSecret)
            .Apply(t => ImmutableDictionary<string, string>.Empty
                .Add("orders.postman", t.Item1 ?? "")
                .Add("aggregator", t.Item2 ?? ""));
    }
}