# ERD-діаграми

Позначки: PK - первинний ключ, FK - зовнішній ключ, UK - унікальне значення.
Між базами немає жодних FOREIGN KEY: `Products.Id` у БД Замовлень - це звичайний
стовпець (локальна копія довідника з Каталогу, той самий Id).

## Проєкт №1: БД Замовлень (SQL Server, ADO.NET + Dapper)

```mermaid
erDiagram
    Customers ||--o{ Orders : "1:N - клієнт має багато замовлень"
    Orders ||--|| OrderDetails : "1:1 - PK деталей є FK на замовлення"
    Orders ||--o{ OrderItems : "M:N через OrderItems"
    Products ||--o{ OrderItems : "M:N через OrderItems"

    Customers {
        BIGINT Id PK
        NVARCHAR Email UK
        NVARCHAR FullName
        DATETIME2 CreatedAt
        BIT IsDeleted
    }
    Orders {
        BIGINT Id PK
        BIGINT CustomerId FK
        NVARCHAR Status "CHECK: допустимі статуси"
        DATETIME2 OrderDate
        BIT IsDeleted
        ROWVERSION RowVersion
    }
    OrderDetails {
        BIGINT OrderId "PK, FK"
        NVARCHAR DeliveryEmail
        NVARCHAR Comment
    }
    OrderItems {
        BIGINT OrderId "PK, FK"
        BIGINT ProductId "PK, FK"
        NVARCHAR ProductName "знімок на момент замовлення"
        DECIMAL UnitPrice "знімок на момент замовлення"
        DECIMAL Discount
        INT Units
    }
    Products {
        BIGINT Id PK "Id з БД Каталогу, не IDENTITY"
        NVARCHAR Name "копія, оновлюється подіями"
        DECIMAL Price "копія, оновлюється подіями"
        INT Stock
        ROWVERSION RowVersion
    }
```

## Проєкт №2: БД Каталогу (SQL Server, EF Core Code First)

```mermaid
erDiagram
    Products ||--o| ProductDetails : "1:1 - PK деталей є FK на товар"
    Products ||--o{ ProductImages : "1:N - товар має багато зображень"
    Products ||--o{ ProductCategories : "M:N через ProductCategories"
    Categories ||--o{ ProductCategories : "M:N через ProductCategories"

    Products {
        INT Id PK
        NVARCHAR Name
        NVARCHAR Sku UK
        DECIMAL Price "CHECK: Price > 0"
        INT AvailableStock "CHECK: >= 0"
    }
    ProductDetails {
        INT ProductId "PK, FK"
        NVARCHAR Description
        NVARCHAR Manufacturer
    }
    ProductImages {
        INT Id PK
        INT ProductId FK
        NVARCHAR Url
        INT SortOrder "UNIQUE разом із ProductId"
    }
    Categories {
        INT Id PK
        NVARCHAR Name UK
    }
    ProductCategories {
        INT ProductId "PK, FK"
        INT CategoryId "PK, FK"
        DATETIME2 AddedAt "власне поле M:N"
    }
```