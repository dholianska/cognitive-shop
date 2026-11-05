using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Власний серіалізатор Value Object: Rating <-> int32.</summary>
public sealed class RatingSerializer : SerializerBase<Rating>
{
    public override Rating Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        => new(context.Reader.ReadInt32());   // конструктор знову перевіряє діапазон 1..5

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Rating value)
        => context.Writer.WriteInt32(value.Value);
}