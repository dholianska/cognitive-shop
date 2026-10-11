/* =====================================================================
   Проєкт №1 - збережувані процедури БД Замовлень
   Коди керованих помилок (THROW): 50001 - не знайдено,
   50002 - конфлікт бізнес-правила, 50003 - некоректні вхідні дані.
   Ці номери перехоплює DAL (Лаб. 02) і перетворює на доменні винятки.
   ===================================================================== */
USE OrdersDb;
GO

/* ---------- 1. Створення клієнта (CRUD: Create) ----------
   Дубль Email порушить UNIQUE (помилка 2627) - її також обробляє DAL. */
CREATE OR ALTER PROCEDURE dbo.usp_CreateCustomer
    @Email     NVARCHAR(256),
    @FullName  NVARCHAR(200),
    @CreatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Email IS NULL OR LTRIM(RTRIM(@Email)) = N''
       OR @FullName IS NULL OR LTRIM(RTRIM(@FullName)) = N''
        THROW 50003, 'Email and FullName are required', 1;

    INSERT dbo.Customers (Email, FullName, CreatedBy)
    VALUES (@Email, @FullName, @CreatedBy);

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;   -- Id нового клієнта
END
GO

/* ---------- 2. М'яке видалення клієнта (CRUD: Delete) ----------
   Рядок не стираємо, а ставимо IsDeleted = 1. Не можна, якщо в клієнта
   є незавершені замовлення. */
CREATE OR ALTER PROCEDURE dbo.usp_SoftDeleteCustomer
    @CustomerId BIGINT,
    @UpdatedBy  NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WITH (UPDLOCK)
                   WHERE Id = @CustomerId AND IsDeleted = 0)
        THROW 50001, 'Customer not found', 1;

    IF EXISTS (SELECT 1 FROM dbo.Orders
               WHERE CustomerId = @CustomerId AND IsDeleted = 0
                 AND Status NOT IN ('Shipped', 'Cancelled'))
        THROW 50002, 'Customer has unfinished orders', 1;

    UPDATE dbo.Customers
    SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE Id = @CustomerId;

    COMMIT TRANSACTION;
END
GO

/* ---------- 3. Створення замовлення (транзакційна бізнес-операція) ----------
   @ItemsJson - JSON-масив позицій: [{"productId":1,"units":2}, ...]
   Три таблиці (Orders, OrderDetails, OrderItems) заповнюються в одній
   транзакції. ProductName і UnitPrice беруться з Products як ЗНІМОК. */
CREATE OR ALTER PROCEDURE dbo.usp_CreateOrder
    @CustomerId    BIGINT,
    @DeliveryEmail NVARCHAR(256),
    @ItemsJson     NVARCHAR(MAX),
    @Comment       NVARCHAR(1000) = NULL,
    @CreatedBy     NVARCHAR(100)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId AND IsDeleted = 0)
        THROW 50001, 'Customer not found', 1;

    IF @ItemsJson IS NULL OR ISJSON(@ItemsJson) <> 1
        THROW 50003, 'Items must be a valid JSON array', 1;

    -- розбираємо JSON; однакові товари складаємо в одну позицію
    DECLARE @Items TABLE (ProductId BIGINT NULL, Units INT NULL);
    INSERT @Items (ProductId, Units)
    SELECT ProductId, SUM(Units)
    FROM OPENJSON(@ItemsJson)
         WITH (ProductId BIGINT '$.productId', Units INT '$.units')
    GROUP BY ProductId;

    IF NOT EXISTS (SELECT 1 FROM @Items)
        THROW 50003, 'Order must contain at least one item', 1;

    IF EXISTS (SELECT 1 FROM @Items WHERE ProductId IS NULL OR Units IS NULL OR Units <= 0)
        THROW 50003, 'Each item needs productId and positive units', 1;

    IF EXISTS (SELECT 1
               FROM @Items i
               LEFT JOIN dbo.Products p ON p.Id = i.ProductId AND p.IsDeleted = 0
               WHERE p.Id IS NULL)
        THROW 50001, 'One or more products not found', 1;

    BEGIN TRANSACTION;

    INSERT dbo.Orders (CustomerId, CreatedBy)
    VALUES (@CustomerId, @CreatedBy);

    DECLARE @OrderId BIGINT = CAST(SCOPE_IDENTITY() AS BIGINT);

    INSERT dbo.OrderDetails (OrderId, DeliveryEmail, Comment)
    VALUES (@OrderId, @DeliveryEmail, @Comment);

    INSERT dbo.OrderItems (OrderId, ProductId, ProductName, UnitPrice, Discount, Units)
    SELECT @OrderId, p.Id, p.Name, p.Price, 0, i.Units     -- знімок назви й ціни
    FROM @Items i
    JOIN dbo.Products p ON p.Id = i.ProductId;

    COMMIT TRANSACTION;

    SELECT @OrderId AS Id;   -- Id нового замовлення
END
GO

/* ---------- 4. Читання замовлення (CRUD: Read) ----------
   Повертає два набори результатів: (1) замовлення з деталями, (2) позиції. */
CREATE OR ALTER PROCEDURE dbo.usp_GetOrderWithItems
    @OrderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId AND IsDeleted = 0)
        THROW 50001, 'Order not found', 1;

    SELECT o.Id, o.CustomerId, o.Status, o.OrderDate,
           d.DeliveryEmail, d.Comment
    FROM dbo.Orders o
    LEFT JOIN dbo.OrderDetails d ON d.OrderId = o.Id
    WHERE o.Id = @OrderId;

    SELECT OrderId, ProductId, ProductName, UnitPrice, Discount, Units
    FROM dbo.OrderItems
    WHERE OrderId = @OrderId;
END
GO

/* ---------- 5. Підтвердження замовлення (з методички Лаб. 01) ----------
   Явна транзакція, відмова на недопустимому переході. */
CREATE OR ALTER PROCEDURE dbo.usp_ConfirmOrder
    @OrderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;   -- будь-яка помилка відкочує транзакцію

    BEGIN TRANSACTION;

    -- UPDLOCK блокує рядок до кінця транзакції
    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50001, 'Order not found', 1;

    IF @Status NOT IN ('Submitted', 'AwaitingValidation')
        THROW 50002, 'Stock can be confirmed only for a new order', 1;

    UPDATE dbo.Orders
    SET Status = 'StockConfirmed', UpdatedAt = SYSUTCDATETIME()
    WHERE Id = @OrderId;

    COMMIT TRANSACTION;
END
GO

/* ---------- 6. Універсальна зміна статусу ----------
   Допустимі переходи описані таблицею нижче. Скасувати (Cancelled)
   можна лише до оплати. Усе інше відхиляється керованою помилкою 50002,
   наприклад, "відвантажити" неоплачене замовлення. */
CREATE OR ALTER PROCEDURE dbo.usp_ChangeOrderStatus
    @OrderId   BIGINT,
    @NewStatus NVARCHAR(30),
    @UpdatedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @Current NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Current IS NULL
        THROW 50001, 'Order not found', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM (VALUES
            ('Submitted',          'AwaitingValidation'),
            ('Submitted',          'StockConfirmed'),
            ('Submitted',          'Cancelled'),
            ('AwaitingValidation', 'StockConfirmed'),
            ('AwaitingValidation', 'Cancelled'),
            ('StockConfirmed',     'Paid'),
            ('StockConfirmed',     'Cancelled'),
            ('Paid',               'Shipped')
        ) AS t (FromStatus, ToStatus)
        WHERE t.FromStatus = @Current AND t.ToStatus = @NewStatus)
    BEGIN
        DECLARE @Message NVARCHAR(200) =
            CONCAT('Transition ', @Current, ' -> ', @NewStatus, ' is not allowed');
        THROW 50002, @Message, 1;
    END

    UPDATE dbo.Orders
    SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE Id = @OrderId;

    COMMIT TRANSACTION;
END
GO