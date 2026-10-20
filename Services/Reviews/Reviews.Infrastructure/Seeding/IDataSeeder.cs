namespace Reviews.Infrastructure.Seeding;

/// <summary>Наповнення колекції початковими даними. Реалізація має бути ідемпотентною.</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}