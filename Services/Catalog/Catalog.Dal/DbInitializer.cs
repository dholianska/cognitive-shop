using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

/// <summary>
/// Програмний сідінг, ідемпотентний: якщо товари вже є, нічого не робить.
/// Категорії засіяні статично (HasData у CategoryConfiguration), тому вони існують до цього коду
/// (спершу довідники, потім залежні записи). Товари додаємо по одному на порожню таблицю,
/// тому Id = 1..5 збігаються з копією довідника в БД Замовлень (Лаб. 01-02).
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct)) return;   // уже наповнено

        var seed = new[]
        {
            (Name: "Тренажер уваги: тест Струпа", Sku: "STROOP-01", Price: 149.00m, Stock: 100,
             Description: "Класичне завдання на вибірковість уваги: назвіть колір слова, ігноруючи його значення.",
             Manufacturer: "CogniLab",
             Images: new[] { "/img/stroop-1.png", "/img/stroop-2.png" }, Categories: new[] { 1, 3 }),

            (Name: "N-back: тренування робочої пам'яті", Sku: "NBACK-01", Price: 199.00m, Stock: 100,
             Description: "Запам'ятовуйте послідовність стимулів і відповідайте, чи збігається поточний із n кроків тому.",
             Manufacturer: "CogniLab",
             Images: new[] { "/img/nback-1.png" }, Categories: new[] { 2 }),

            (Name: "Тест часу реакції", Sku: "REACTION-01", Price: 99.00m, Stock: 200,
             Description: "Серія коротких проб на швидкість простої та вибіркової реакції.",
             Manufacturer: "MindMetrics",
             Images: new[] { "/img/reaction-1.png" }, Categories: new[] { 3 }),

            (Name: "Просторове мислення: ментальне обертання", Sku: "ROTATION-01", Price: 179.00m, Stock: 50,
             Description: "Порівнюйте обернені фігури та визначайте, чи збігаються вони.",
             Manufacturer: "MindMetrics",
             Images: new[] { "/img/rotation-1.png" }, Categories: new[] { 4 }),

            (Name: "Фокус-сесії за технікою Помодоро", Sku: "POMODORO-01", Price: 49.00m, Stock: 300,
             Description: "Структуровані фокус-інтервали з перервами та самооцінкою концентрації.",
             Manufacturer: "FocusWorks",
             Images: new[] { "/img/pomodoro-1.png" }, Categories: new[] { 1, 5 })
        };

        foreach (var s in seed)
        {
            // правила сутності діють і тут: конструктор і методи перевіряють дані
            var product = new Product(s.Name, s.Sku, s.Price);
            product.AddStock(s.Stock);
            product.SetDetail(s.Description, s.Manufacturer);
            for (var i = 0; i < s.Images.Length; i++)
                product.AddImage(s.Images[i], i);
            foreach (var categoryId in s.Categories)
                product.AddToCategory(categoryId);

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);   // по одному, щоб Id відповідали порядку 1..5
        }
    }
}