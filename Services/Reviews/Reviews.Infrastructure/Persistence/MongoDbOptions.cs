namespace Reviews.Infrastructure.Persistence;

/// <summary>Секція MongoDb в appsettings.json.</summary>
public class MongoDbOptions
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
}