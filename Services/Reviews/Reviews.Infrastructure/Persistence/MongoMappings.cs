using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// BSON-мапінг доменних класів без жодних атрибутів у Domain.
/// Реєструється ОДИН раз під час старту, до першого звернення до бази.
/// Імена полів: camelCase, як у документі з Лаб. 01.
/// </summary>
public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            // єдина конвенція імен для доменних типів: camelCase, зайві поля документа ігноруються
            ConventionRegistry.Register(
                "ReviewsConventions",
                new ConventionPack
                {
                    new CamelCaseElementNameConvention(),
                    new IgnoreExtraElementsConvention(true)
                },
                type => type.Namespace?.StartsWith("Reviews.Domain", StringComparison.Ordinal) == true);

            // спільні поля всіх сутностей
            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.MapIdProperty(e => e.Id);   // _id
                cm.MapProperty(e => e.CreatedAt).SetElementName("createdAt");
                cm.MapProperty(e => e.UpdatedAt).SetElementName("updatedAt").SetIgnoreIfNull(true);
                cm.MapProperty(e => e.Version).SetElementName("version");
                cm.SetIgnoreExtraElements(true);
            });

            // Value Object: незмінний, створюється через конструктор (MapCreator)
            BsonClassMap.RegisterClassMap<Author>(cm =>
            {
                cm.MapProperty(a => a.UserId).SetElementName("userId");
                cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
                cm.MapCreator(a => new Author(a.UserId, a.DisplayName));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Comment>(cm =>
            {
                cm.MapProperty(c => c.Author).SetElementName("author");
                cm.MapProperty(c => c.Text).SetElementName("text");
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Review>(cm =>
            {
                // productId зберігається як int32 (як у Лаб. 01), хоча в коді це long
                cm.MapProperty(r => r.ProductId).SetElementName("productId")
                    .SetSerializer(new Int64Serializer(BsonType.Int32));
                cm.MapProperty(r => r.ProductName).SetElementName("productName");
                cm.MapProperty(r => r.Author).SetElementName("author");
                cm.MapProperty(r => r.Rating).SetElementName("rating")
                    .SetSerializer(new RatingSerializer());
                cm.MapProperty(r => r.Text).SetElementName("text");
                cm.MapField("_comments").SetElementName("comments");   // приватне поле -> масив comments
                cm.SetIgnoreExtraElements(true);
            });

            _registered = true;
        }
    }
}