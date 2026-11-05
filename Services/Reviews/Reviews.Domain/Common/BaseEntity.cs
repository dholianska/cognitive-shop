using MongoDB.Bson;

namespace Reviews.Domain.Common;

/// <summary>
/// Базова сутність для MongoDB: ідентифікатор ObjectId, службові дати й версія документа.
/// Сеттери захищені: змінювати стан можна лише через методи самих сутностей.
/// </summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    /// <summary>Номер версії документа для оптимістичної конкурентності.</summary>
    public int Version { get; protected set; }

    protected void Touch() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Збільшує версію. Викликає репозиторій перед записом (див. ReviewRepository).</summary>
    public void BumpVersion() => Version++;
}