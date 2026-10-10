/* =====================================================================
   Проєкт №2 - початкові дані Каталогу. Ідемпотентний.
   Id товарів 1-5 збігаються з довідником Products у БД Замовлень.
   ===================================================================== */
USE CatalogDb;
GO

/* ---------- Категорії (ключ перевірки - унікальна Name) ---------- */
INSERT dbo.Categories (Name)
SELECT v.Name
FROM (VALUES (N'Увага'), (N'Пам''ять'), (N'Швидкість реакції'),
             (N'Просторове мислення'), (N'Фокус і продуктивність')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories c WHERE c.Name = v.Name);
GO

/* ---------- Товари з фіксованими Id (ключ перевірки - Id) ---------- */
SET IDENTITY_INSERT dbo.Products ON;

INSERT dbo.Products (Id, Name, Sku, Price, AvailableStock)
SELECT v.Id, v.Name, v.Sku, v.Price, v.Stock
FROM (VALUES
    (1, N'Тренажер уваги: тест Струпа',                 N'STROOP-01',   149.00, 100),
    (2, N'N-back: тренування робочої пам''яті',         N'NBACK-01',    199.00, 100),
    (3, N'Тест часу реакції',                           N'REACTION-01',  99.00, 200),
    (4, N'Просторове мислення: ментальне обертання',    N'ROTATION-01', 179.00,  50),
    (5, N'Фокус-сесії за технікою Помодоро',            N'POMODORO-01',  49.00, 300)
) AS v(Id, Name, Sku, Price, Stock)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Id = v.Id);

SET IDENTITY_INSERT dbo.Products OFF;
GO

/* ---------- Деталі товарів, 1:1 (ключ перевірки - ProductId) ---------- */
INSERT dbo.ProductDetails (ProductId, Description, Manufacturer)
SELECT v.ProductId, v.Description, v.Manufacturer
FROM (VALUES
    (1, N'Класичне завдання на вибірковість уваги: назвіть колір слова, ігноруючи його значення.', N'CogniLab'),
    (2, N'Запам''ятовуйте послідовність стимулів і відповідайте, чи збігається поточний із n кроків тому.', N'CogniLab'),
    (3, N'Серія коротких проб на швидкість простої та вибіркової реакції.', N'MindMetrics'),
    (4, N'Порівнюйте обернені фігури та визначайте, чи збігаються вони.', N'MindMetrics'),
    (5, N'Структуровані фокус-інтервали з перервами та самооцінкою концентрації.', N'FocusWorks')
) AS v(ProductId, Description, Manufacturer)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductDetails d WHERE d.ProductId = v.ProductId);
GO

/* ---------- Зображення, 1:N (ключ перевірки - ProductId + SortOrder) ---------- */
INSERT dbo.ProductImages (ProductId, Url, SortOrder)
SELECT v.ProductId, v.Url, v.SortOrder
FROM (VALUES
    (1, N'/img/stroop-1.png',   0),
    (1, N'/img/stroop-2.png',   1),
    (2, N'/img/nback-1.png',    0),
    (3, N'/img/reaction-1.png', 0),
    (4, N'/img/rotation-1.png', 0),
    (5, N'/img/pomodoro-1.png', 0)
) AS v(ProductId, Url, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImages i
                  WHERE i.ProductId = v.ProductId AND i.SortOrder = v.SortOrder);
GO

/* ---------- Товар - категорія, M:N (ключ перевірки - пара ProductId + CategoryId) ---------- */
INSERT dbo.ProductCategories (ProductId, CategoryId)
SELECT v.ProductId, c.Id
FROM (VALUES
    (1, N'Увага'),
    (1, N'Швидкість реакції'),
    (2, N'Пам''ять'),
    (3, N'Швидкість реакції'),
    (4, N'Просторове мислення'),
    (5, N'Увага'),
    (5, N'Фокус і продуктивність')
) AS v(ProductId, CategoryName)
JOIN dbo.Categories c ON c.Name = v.CategoryName
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategories pc
                  WHERE pc.ProductId = v.ProductId AND pc.CategoryId = c.Id);
GO