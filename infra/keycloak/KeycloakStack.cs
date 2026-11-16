using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

public class KeycloakStack : Stack
{
    public const string RealmName = "DevRealmDrivers";

    [Output] public Output<string> RealmUrl { get; set; } = null!;
    [Output] public Output<string> AdminConsoleUrl { get; set; } = null!;

    public KeycloakStack()
    {
        var config = new Pulumi.Config();
        var keycloakUrl = config.Get("keycloakUrl") ?? "http://localhost:8080";

        // Provider з admin-credentials Keycloak з AppHost (лише для розробки; для інших стендів: pulumi config set --secret adminPassword ...)
        var provider = new Keycloak.Provider("keycloak", new Keycloak.ProviderArgs
        {
            Url = keycloakUrl,
            ClientId = "admin-cli",
            Username = config.Get("adminUsername") ?? "admin",
            Password = config.GetSecret("adminPassword") ?? Output.CreateSecret("admin123"),
            InitialLogin = false   // pulumi preview працює і без запущеного Keycloak
        });
        var opts = new CustomResourceOptions { Provider = provider };

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

        RealmUrl = Output.Format($"{keycloakUrl}/realms/{realm.RealmName}");
        AdminConsoleUrl = Output.Create($"{keycloakUrl}/admin/master/console/#/{RealmName}");
    }
}