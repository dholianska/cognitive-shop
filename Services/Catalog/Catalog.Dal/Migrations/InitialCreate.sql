IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Categories] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
);

CREATE TABLE [Products] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [Sku] nvarchar(50) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [AvailableStock] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Products_Price] CHECK ([Price] > 0),
    CONSTRAINT [CK_Products_Stock] CHECK ([AvailableStock] >= 0)
);

CREATE TABLE [ProductCategories] (
    [ProductId] int NOT NULL,
    [CategoryId] int NOT NULL,
    [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([ProductId], [CategoryId]),
    CONSTRAINT [FK_ProductCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductCategories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductDetails] (
    [ProductId] int NOT NULL,
    [Description] nvarchar(2000) NOT NULL,
    [Manufacturer] nvarchar(200) NULL,
    CONSTRAINT [PK_ProductDetails] PRIMARY KEY ([ProductId]),
    CONSTRAINT [FK_ProductDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ProductImages] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    [SortOrder] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
    SET IDENTITY_INSERT [Categories] ON;
INSERT INTO [Categories] ([Id], [Name])
VALUES (1, N'Увага'),
(2, N'Пам''ять'),
(3, N'Швидкість реакції'),
(4, N'Просторове мислення'),
(5, N'Фокус і продуктивність');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name') AND [object_id] = OBJECT_ID(N'[Categories]'))
    SET IDENTITY_INSERT [Categories] OFF;

CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);

CREATE INDEX [IX_ProductCategories_CategoryId_AddedAt] ON [ProductCategories] ([CategoryId], [AddedAt] DESC);

CREATE UNIQUE INDEX [IX_ProductImages_ProductId_SortOrder] ON [ProductImages] ([ProductId], [SortOrder]);

CREATE INDEX [IX_Products_Name] ON [Products] ([Name]);

CREATE UNIQUE INDEX [IX_Products_Sku] ON [Products] ([Sku]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261015135538_InitialCreate', N'9.0.21');

COMMIT;
GO

