using Aspire.Hosting.ApplicationModel;

/// <summary>Передає однакові параметри власного JWT (Лаб. 09, Ч.1) в усі сервіси, які його валідують.</summary>
internal static class JwtEnvironmentExtensions
{
    public static IResourceBuilder<T> WithOwnJwt<T>(
        this IResourceBuilder<T> builder, IResourceBuilder<ParameterResource> secret)
        where T : IResourceWithEnvironment
        => builder
            .WithEnvironment("Jwt__Issuer", "cognitive-shop-auth")
            .WithEnvironment("Jwt__Audience", "cognitive-shop")
            .WithEnvironment("Jwt__SecretKey", secret);

    public static IResourceBuilder<T> WithIdentityServerAuthority<T>(this IResourceBuilder<T> builder)
    where T : IResourceWithEnvironment
    => builder.WithEnvironment("IdentityServer__Authority", "http://localhost:5100");
}