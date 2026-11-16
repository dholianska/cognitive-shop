using System.Collections.Generic;

namespace KeycloakInfra;

/// <summary>Єдине джерело правди: таблиці 6 і 7 з методички.</summary>
public static class PermissionMatrix
{
    public static readonly string[] Roles = { "admin", "manager", "user", "guest" };

    // Таблиця 6: resource servers і повний набір scopes (permissions)
    public static readonly Dictionary<string, string[]> ResourceServers = new()
    {
        ["orders-api"] = new[] { "orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve" },
        ["products-api"] = new[] { "products:read", "products:create", "products:update", "products:delete", "products:publish" },
        ["payments-api"] = new[] { "payments:read", "payments:process", "payments:refund" }
    };

    // Таблиця 7: що роль отримує на resource server. Немає запису = немає доступу.
    // payments-api отримує ЛИШЕ admin: свідоме рішення, а не недогляд.
    public static readonly (string Role, string Server, string[] Scopes)[] Grants =
    {
        ("admin", "orders-api", ResourceServers["orders-api"]),
        ("admin", "products-api", ResourceServers["products-api"]),
        ("admin", "payments-api", ResourceServers["payments-api"]),

        ("manager", "orders-api", new[] { "orders:read", "orders:update", "orders:approve" }),
        ("manager", "products-api", new[] { "products:read", "products:update", "products:publish" }),

        ("user", "orders-api", new[] { "orders:read", "orders:create" }),
        ("user", "products-api", new[] { "products:read" }),

        ("guest", "products-api", new[] { "products:read" })
    };
}