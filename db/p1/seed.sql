/* =====================================================================
   Проєкт №1 - початкові дані БД Замовлень.
   Ідемпотентний: перед кожною вставкою перевіряється наявність запису,
   тому повторний запуск не створює дублікатів.
   ===================================================================== */
USE OrdersDb;
GO

/* ---------- Клієнти (ключ перевірки - унікальний Email) ---------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'olena.koval@example.com')
    INSERT dbo.Customers (Email, FullName, CreatedBy)
    VALUES (N'olena.koval@example.com', N'Олена Коваль', N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'andriy.melnyk@example.com')
    INSERT dbo.Customers (Email, FullName, CreatedBy)
    VALUES (N'andriy.melnyk@example.com', N'Андрій Мельник', N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'maria.shevchenko@example.com')
    INSERT dbo.Customers (Email, FullName, CreatedBy)
    VALUES (N'maria.shevchenko@example.com', N'Марія Шевченко', N'seed');
GO

/* ---------- Локальна копія довідника товарів ----------
   Id збігаються з БД Каталогу (db/p2/seed.sql). Stock - кількість
   доступних ліцензій на вправу. Ключ перевірки - Id. */
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = 1)
    INSERT dbo.Products (Id, Name, Price, Stock, CreatedBy)
    VALUES (1, N'Тренажер уваги: тест Струпа', 149.00, 100, N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = 2)
    INSERT dbo.Products (Id, Name, Price, Stock, CreatedBy)
    VALUES (2, N'N-back: тренування робочої пам''яті', 199.00, 100, N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = 3)
    INSERT dbo.Products (Id, Name, Price, Stock, CreatedBy)
    VALUES (3, N'Тест часу реакції', 99.00, 200, N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = 4)
    INSERT dbo.Products (Id, Name, Price, Stock, CreatedBy)
    VALUES (4, N'Просторове мислення: ментальне обертання', 179.00, 50, N'seed');

IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = 5)
    INSERT dbo.Products (Id, Name, Price, Stock, CreatedBy)
    VALUES (5, N'Фокус-сесії за технікою Помодоро', 49.00, 300, N'seed');
GO

/* ---------- Одне демонстраційне замовлення ----------
   Створюється через збережену процедуру (заодно її перевіряє), лише якщо
   замовлень у базі ще немає. */
IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    DECLARE @CustomerId BIGINT =
        (SELECT Id FROM dbo.Customers WHERE Email = N'olena.koval@example.com');

    EXEC dbo.usp_CreateOrder
        @CustomerId    = @CustomerId,
        @DeliveryEmail = N'olena.koval@example.com',
        @ItemsJson     = N'[{"productId":1,"units":1},{"productId":2,"units":1}]',
        @Comment       = N'Демонстраційне замовлення',
        @CreatedBy     = N'seed';
END
GO