using System.Collections.Generic;
using System.Linq;
using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

public static class ResourceServers
{
    public static Dictionary<string, Keycloak.Role> CreateRoles(Keycloak.Realm realm, CustomResourceOptions opts)
        => PermissionMatrix.Roles.ToDictionary(
            name => name,
            name => new Keycloak.Role($"role-{name}", new Keycloak.RoleArgs
            {
                RealmId = realm.Id,
                Name = name,
                Description = $"Realm role '{name}' (permission matrix)"
            }, opts));

    public static Dictionary<string, Keycloak.OpenId.Client> CreateAll(
        Keycloak.Realm realm, IReadOnlyDictionary<string, Keycloak.Role> roles, CustomResourceOptions opts)
    {
        var servers = new Dictionary<string, Keycloak.OpenId.Client>();
        foreach (var (name, scopes) in PermissionMatrix.ResourceServers)
            servers[name] = Create(realm, name, scopes, roles, opts);
        return servers;
    }

    private static Keycloak.OpenId.Client Create(
        Keycloak.Realm realm, string name, string[] scopes,
        IReadOnlyDictionary<string, Keycloak.Role> roles, CustomResourceOptions opts)
    {
        // 1) Resource server = client з увімкненою Authorization
        var client = new Keycloak.OpenId.Client($"rs-{name}", new Keycloak.OpenId.ClientArgs
        {
            RealmId = realm.Id,
            ClientId = name,
            Name = name,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,            // потрібно для Authorization Services
            StandardFlowEnabled = false,
            Authorization = new Keycloak.OpenId.Inputs.ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",    // Permission Evaluation mode: без permission доступу немає
                // AFFIRMATIVE: достатньо одного permission, що дозволяє. З UNANIMOUS роль user
                // програвала б, бо на той самий scope діє ще й permission адміна.
                DecisionStrategy = "AFFIRMATIVE",
                KeepDefaults = false                    // прибираємо Default Resource/Policy/Permission
            }
        }, opts);

        // 2) повний набір scopes
        var scopeResources = scopes.ToDictionary(
            s => s,
            s => new Keycloak.OpenId.ClientAuthorizationScope($"{name}-scope-{s.Replace(':', '-')}",
                new Keycloak.OpenId.ClientAuthorizationScopeArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = client.ResourceServerId,
                    Name = s
                }, opts));

        // 3) resource з ім'ям сервісу: це rsname у claim authorization.permissions
        var resource = new Keycloak.OpenId.ClientAuthorizationResource($"{name}-resource",
            new Keycloak.OpenId.ClientAuthorizationResourceArgs
            {
                RealmId = realm.Id,
                ResourceServerId = client.ResourceServerId,
                Name = name,
                DisplayName = name,
                Scopes = scopeResources.Values.Select(s => s.Name).ToArray()
            }, opts);

        // 4) для кожної ролі, що має доступ: role policy + scope-based permission
        foreach (var (role, _, granted) in PermissionMatrix.Grants.Where(g => g.Server == name))
        {
            var policy = new Keycloak.OpenId.ClientRolePolicy($"{name}-policy-{role}",
                new Keycloak.OpenId.ClientRolePolicyArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = client.ResourceServerId,
                    Name = $"{role}-role-policy",
                    Type = "role",
                    Logic = "POSITIVE",
                    DecisionStrategy = "UNANIMOUS",
                    Roles = new[]
                    {
                        new Keycloak.OpenId.Inputs.ClientRolePolicyRoleArgs { Id = roles[role].Id, Required = true }
                    }
                }, opts);

            _ = new Keycloak.OpenId.ClientAuthorizationPermission($"{name}-permission-{role}",
                new Keycloak.OpenId.ClientAuthorizationPermissionArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = client.ResourceServerId,
                    Name = $"{name}-{role}-permission",
                    Type = "scope",
                    DecisionStrategy = "UNANIMOUS",
                    Resources = new[] { resource.Id },
                    Scopes = granted.Select(s => scopeResources[s].Id).ToArray(),
                    Policies = new[] { policy.Id }
                }, opts);
        }

        return client;
    }
}