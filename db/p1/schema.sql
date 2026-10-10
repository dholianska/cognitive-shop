/* =====================================================================
   Проєкт №1 - БД Замовлень (SQL Server, ADO.NET + Dapper)
   Ідемпотентний скрипт: повторний запуск не ламає і не дублює об'єкти.
   Між цією БД і БД Каталогу немає жодних FOREIGN KEY: Products - це
   локальна копія довідника з тим самим Id (див. політику дублювання в README).
   ===================================================================== */

IF DB_ID(N'OrdersDb') IS NULL
    CREATE DATABASE OrdersDb;
GO

USE OrdersDb;
GO

/* ---------------------------------------------------------------------
   Customers: покупці.
   PK BIGINT IDENTITY: ідентифікатори створює лише ця БД (одне джерело
   вставки), тому послідовний числовий ключ простіший і компактніший за GUID.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
CREATE TABLE dbo.Customers (
    Id        BIGINT IDENTITY(1,1) CONSTRAINT PK_Customers PRIMARY KEY,
    Email     NVARCHAR(256) NOT NULL CONSTRAINT UQ_Customers_Email UNIQUE,
    FullName  NVARCHAR(200) NOT NULL,
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL,
    UpdatedAt DATETIME2     NULL,
    UpdatedBy NVARCHAR(100) NULL,
    IsDeleted BIT           NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT 0
);
GO

/* ---------------------------------------------------------------------
   Products: ЛОКАЛЬНА КОПІЯ довідника з БД Каталогу (власник даних - Каталог).
   Id НЕ є IDENTITY: це той самий Id, що в Каталозі. FK на іншу БД немає.
   Name і Price оновлюються подіями з Каталогу (див. політику дублювання).
   RowVersion: залишок Stock можуть змінювати паралельно кілька замовлень.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Products (
    Id         BIGINT        NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    Name       NVARCHAR(200) NOT NULL,
    Price      DECIMAL(18,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    Stock      INT           NOT NULL CONSTRAINT DF_Products_Stock DEFAULT 0
                                      CONSTRAINT CK_Products_Stock CHECK (Stock >= 0),
    CreatedAt  DATETIME2     NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2     NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT           NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT 0,
    RowVersion ROWVERSION
);
GO

/* ---------------------------------------------------------------------
   Orders: зв'язок 1:N (Customers -> Orders).
   FK без каскадного видалення: історія замовлень не має зникати разом
   із клієнтом (для клієнтів використовуємо м'яке видалення IsDeleted).
   RowVersion: два користувачі не повинні мовчки затерти зміни статусу.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.Orders (
    Id         BIGINT IDENTITY(1,1) CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId BIGINT      NOT NULL
               CONSTRAINT FK_Orders_Customers REFERENCES dbo.Customers(Id),
    Status     NVARCHAR(30) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT 'Submitted'
               CONSTRAINT CK_Orders_Status CHECK (Status IN
                   ('Submitted','AwaitingValidation','StockConfirmed','Paid','Shipped','Cancelled')),
    OrderDate  DATETIME2    NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    CreatedAt  DATETIME2    NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2    NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT          NOT NULL CONSTRAINT DF_Orders_IsDeleted DEFAULT 0,
    RowVersion ROWVERSION
);
GO

/* ---------------------------------------------------------------------
   OrderDetails: зв'язок 1:1 (Orders <-> OrderDetails).
   PK одночасно є FK на Orders, тому для одного замовлення може бути
   лише один запис деталей. Каскад доречний: деталі не існують без замовлення.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.OrderDetails', N'U') IS NULL
CREATE TABLE dbo.OrderDetails (
    OrderId       BIGINT NOT NULL CONSTRAINT PK_OrderDetails PRIMARY KEY
                  CONSTRAINT FK_OrderDetails_Orders REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    DeliveryEmail NVARCHAR(256)  NOT NULL,   -- куди надіслати доступ до вправ
    Comment       NVARCHAR(1000) NULL
);
GO

/* ---------------------------------------------------------------------
   OrderItems: зв'язок M:N (Orders <-> Products) через проміжну таблицю
   із двома FK та власними полями.
   ProductName і UnitPrice - ЗНІМОК на момент замовлення, не оновлюються.
   Каскад від Orders доречний (позиції не існують без замовлення);
   FK на Products без каскаду (товар не можна видалити, поки він є в замовленнях).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
CREATE TABLE dbo.OrderItems (
    OrderId     BIGINT NOT NULL
                CONSTRAINT FK_OrderItems_Orders REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    ProductId   BIGINT NOT NULL
                CONSTRAINT FK_OrderItems_Products REFERENCES dbo.Products(Id),
    ProductName NVARCHAR(200) NOT NULL,                       -- знімок
    UnitPrice   DECIMAL(18,2) NOT NULL CONSTRAINT CK_OrderItems_UnitPrice CHECK (UnitPrice > 0), -- знімок
    Discount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrderItems_Discount DEFAULT 0
                              CONSTRAINT CK_OrderItems_Discount CHECK (Discount >= 0),
    Units       INT NOT NULL CONSTRAINT CK_OrderItems_Units CHECK (Units > 0),
    CONSTRAINT PK_OrderItems PRIMARY KEY (OrderId, ProductId)  -- складений ключ: товар в замовленні один раз
);
GO

/* =====================================================================
   ІНДЕКСИ: кожен створено під конкретний запит
   ===================================================================== */

-- Запит: "активні замовлення клієнта з певним статусом"
-- (фільтрований: індексує лише не видалені рядки, тому менший і швидший)
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Orders_CustomerId_Status' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_CustomerId_Status
        ON dbo.Orders (CustomerId, Status) WHERE IsDeleted = 0;
GO

-- Запит: "останні замовлення" (список замовлень, відсортований за датою)
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Orders_OrderDate' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_OrderDate
        ON dbo.Orders (OrderDate DESC) WHERE IsDeleted = 0;
GO

-- Запит: "в яких замовленнях є цей товар" (PK OrderItems починається з OrderId,
-- тому пошук за ProductId без цього індексу переглядав би всю таблицю)
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_OrderItems_ProductId' AND object_id = OBJECT_ID(N'dbo.OrderItems'))
    CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems (ProductId);
GO