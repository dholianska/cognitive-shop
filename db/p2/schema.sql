/* =====================================================================
   Проєкт №2 - БД Каталогу (SQL Server, EF Core Code First у Лаб. 03).
   Тут проєктується схема; у Лаб. 03 її відтворять моделі й міграції EF.
   Ідемпотентний скрипт. FOREIGN KEY на інші бази немає.
   Каталог - ВЛАСНИК даних про товар (Name, Price): їх копії в базах
   Замовлень і Відгуків оновлюються подіями (див. політику дублювання).
   ===================================================================== */

IF DB_ID(N'CatalogDb') IS NULL
    CREATE DATABASE CatalogDb;
GO

USE CatalogDb;
GO

/* ---------- Categories: довідник категорій вправ ---------- */
IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
CREATE TABLE dbo.Categories (
    Id   INT IDENTITY(1,1) CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE
);
GO

/* ---------- Products: вправи й тести (кореневий агрегат) ---------- */
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Products (
    Id             INT IDENTITY(1,1) CONSTRAINT PK_Products PRIMARY KEY,
    Name           NVARCHAR(200) NOT NULL,
    Sku            NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,   -- артикул
    Price          DECIMAL(18,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    AvailableStock INT NOT NULL CONSTRAINT DF_Products_Stock DEFAULT 0
                       CONSTRAINT CK_Products_Stock CHECK (AvailableStock >= 0)
);
GO

/* ---------- ProductDetails: зв'язок 1:1 (Products <-> ProductDetails) ----------
   PK є водночас FK, тому для одного товару - один запис деталей. */
IF OBJECT_ID(N'dbo.ProductDetails', N'U') IS NULL
CREATE TABLE dbo.ProductDetails (
    ProductId    INT NOT NULL CONSTRAINT PK_ProductDetails PRIMARY KEY
                 CONSTRAINT FK_ProductDetails_Products REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    Description  NVARCHAR(2000) NOT NULL,
    Manufacturer NVARCHAR(200)  NULL
);
GO

/* ---------- ProductImages: зв'язок 1:N (Products -> ProductImages) ---------- */
IF OBJECT_ID(N'dbo.ProductImages', N'U') IS NULL
CREATE TABLE dbo.ProductImages (
    Id        INT IDENTITY(1,1) CONSTRAINT PK_ProductImages PRIMARY KEY,
    ProductId INT NOT NULL
              CONSTRAINT FK_ProductImages_Products REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    Url       NVARCHAR(500) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_ProductImages_SortOrder DEFAULT 0
);
GO

/* ---------- ProductCategories: зв'язок M:N із ВЛАСНИМ полем AddedAt ----------
   Проміжна таблиця з двома FK; складений PK не дає додати товар у
   категорію двічі. AddedAt - дата додавання товару в категорію. */
IF OBJECT_ID(N'dbo.ProductCategories', N'U') IS NULL
CREATE TABLE dbo.ProductCategories (
    ProductId  INT NOT NULL
               CONSTRAINT FK_ProductCategories_Products REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    CategoryId INT NOT NULL
               CONSTRAINT FK_ProductCategories_Categories REFERENCES dbo.Categories(Id) ON DELETE CASCADE,
    AddedAt    DATETIME2 NOT NULL CONSTRAINT DF_ProductCategories_AddedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ProductCategories PRIMARY KEY (ProductId, CategoryId)
);
GO

/* =====================================================================
   ІНДЕКСИ: кожен створено під конкретний запит
   (унікальні індекси на Sku й Categories.Name створюють самі UNIQUE-обмеження)
   ===================================================================== */

-- Запит: пошук і сортування товарів за назвою (каталог, фільтр "містить")
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Products_Name' AND object_id = OBJECT_ID(N'dbo.Products'))
    CREATE INDEX IX_Products_Name ON dbo.Products (Name);
GO

-- Запит: "усі товари певної категорії" (PK починається з ProductId,
-- тому пошук за CategoryId без цього індексу переглядав би всю таблицю)
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_ProductCategories_CategoryId' AND object_id = OBJECT_ID(N'dbo.ProductCategories'))
    CREATE INDEX IX_ProductCategories_CategoryId ON dbo.ProductCategories (CategoryId, AddedAt DESC);
GO

-- Запит: "зображення товару в порядку показу"; унікальність (ProductId, SortOrder)
-- не дозволяє двох зображень з однаковим порядком в одного товару
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'UX_ProductImages_Product_Sort' AND object_id = OBJECT_ID(N'dbo.ProductImages'))
    CREATE UNIQUE INDEX UX_ProductImages_Product_Sort ON dbo.ProductImages (ProductId, SortOrder);
GO