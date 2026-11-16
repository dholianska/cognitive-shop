using System.Collections.Generic;
using Pulumi;
using Keycloak = Pulumi.Keycloak;

namespace KeycloakInfra;

public static class TestUsers
{
    public const string Password = "Pass123$";

    public static readonly (string Username, string FirstName, string LastName, string Role)[] Definitions =
    {
        ("alice", "Alice", "Admin", "admin"),
        ("john.manager", "John", "Manager", "manager"),
        ("bob", "Bob", "User", "user"),
        ("guest", "Gina", "Guest", "guest")
    };

    public static void Create(
        Keycloak.Realm realm, IReadOnlyDictionary<string, Keycloak.Role> roles,
        bool temporaryPasswords, CustomResourceOptions opts)
    {
        foreach (var (username, first, last, role) in Definitions)
        {
            var user = new Keycloak.User($"user-{username}", new Keycloak.UserArgs
            {
                RealmId = realm.Id,
                Username = username,
                Email = $"{username}@example.com",
                EmailVerified = true,            // email підтверджено
                Enabled = true,
                FirstName = first,               // без імені/прізвища Keycloak 26 вважає акаунт неповним
                LastName = last,
                InitialPassword = new Keycloak.Inputs.UserInitialPasswordArgs
                {
                    Value = Password,
                    Temporary = temporaryPasswords   // за методичкою true: змінюється при першому вході
                }
            }, opts);

            _ = new Keycloak.UserRoles($"user-roles-{username}", new Keycloak.UserRolesArgs
            {
                RealmId = realm.Id,
                UserId = user.Id,
                RoleIds = new[] { roles[role].Id },
                Exhaustive = false                // не знімати default realm roles
            }, opts);
        }
    }
}