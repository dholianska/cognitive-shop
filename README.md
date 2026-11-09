# Магазин психологічних вправ і тестів

## Предметна область
Онлайн-магазин, у якому користувачі купують доступ до вправ і тестів для
тренування уваги, пам'яті та інших когнітивних функцій. Система веде каталог
вправ, обробляє замовлення з оплатою та зберігає відгуки й результати проходження.

## Обмежені контексти (Bounded Contexts)
| Проєкт | Контекст | Сховище | Сутності, що йому належать |
|---|---|---|---|
| №1 | Замовлення | SQL Server (ADO.NET + Dapper) | Customer, Order, OrderDetails, OrderItem, Product (локальна копія довідника) |
| №2 | Каталог | SQL Server (EF Core) | Product, Category, ProductCategory, ProductDetail, ProductImage |
| №3 | Відгуки | MongoDB | reviews (з вбудованими коментарями), discussions, sessionResults |

Кожен контекст має власну базу даних. **Між базами немає жодного FOREIGN KEY**:
поля `ProductId` у базі Замовлень і у Відгуках, а також `userId` у Відгуках — це
звичайні стовпці/поля без зовнішнього ключа, а цілісність таких посилань за
потреби перевіряє прикладний код.

## Політика дублювання даних
| Поле | Де зберігається | Власник даних | Політика узгодженості |
|---|---|---|---|
| OrderItems.ProductName, UnitPrice | БД Замовлень | Product (БД Каталогу) | знімок на момент оформлення, не оновлюється |
| Products.Name, Price (локальна копія) | БД Замовлень | Product (БД Каталогу) | оновлюється подіями з Каталогу |
| reviews.productName | БД Відгуків (MongoDB) | Product (БД Каталогу) | оновлюється подіями з Каталогу |
| reviews.author.displayName | БД Відгуків (MongoDB) | Customer (БД Замовлень) | знімок на момент написання відгуку, не оновлюється |

## Примітка щодо статусів замовлення
Статуси: Submitted → AwaitingValidation → StockConfirmed → Paid → Shipped,
скасування (Cancelled) можливе лише до оплати. Для цифрового товару Shipped
означає «доступ до вправи надано», а Stock у Products - кількість доступних ліцензій.


## ERD-діаграми
Діаграми зв'язків для Проєкту №1 і №2 лежать у [docs/erd.md](docs/erd.md),
приклади документів і запити MongoDB у [docs/mongo-examples.md](docs/mongo-examples.md).

## Розгортання баз даних з нуля

Потрібні Docker, а також SSMS або Visual Studio (SQL Server Object Explorer) для SQL-скриптів.
Усі скрипти ідемпотентні: повторний запуск не створює дублікатів.

### 1. SQL Server (Проєкти №1 і №2)
```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=<ваш_пароль>" -p 1433:1433 --name sql1 -d mcr.microsoft.com/mssql/server:2022-latest
```
Підключитися до `localhost,1433` (логін `sa`, Trust server certificate) і виконати скрипти **в такому порядку**:

| Крок | Файл | Що робить |
|---|---|---|
| 1 | `db/p1/schema.sql` | створює БД `OrdersDb`, таблиці, індекси |
| 2 | `db/p1/procedures.sql` | створює 6 збережуваних процедур |
| 3 | `db/p1/seed.sql` | початкові дані (клієнти, товари, замовлення) |
| 4 | `db/p2/schema.sql` | створює БД `CatalogDb`, таблиці, індекси |
| 5 | `db/p2/seed.sql` | довідники й товари (Id 1-5 збігаються з `OrdersDb`) |

Кожен файл виконується повністю в одному вікні запиту (кнопка Execute).

### 2. MongoDB (Проєкт №3)
```bash
docker run -d --name mongo1 -p 27017:27017 mongo:7
docker cp db/p3/collections.js mongo1:/tmp/collections.js
docker cp db/p3/seed.js mongo1:/tmp/seed.js
docker exec mongo1 mongosh --quiet --file /tmp/collections.js
docker exec mongo1 mongosh --quiet --file /tmp/seed.js
```

## Перевірка результату

```sql
-- у БД без FOREIGN KEY за межі бази (виконати в OrdersDb і в CatalogDb)
SELECT name, OBJECT_NAME(parent_object_id) AS TableName FROM sys.foreign_keys;

-- кількість записів після seed (OrdersDb): 3 клієнти, 5 товарів, 1 замовлення, 2 позиції
SELECT (SELECT COUNT(*) FROM dbo.Customers) AS Customers, (SELECT COUNT(*) FROM dbo.Products) AS Products,
       (SELECT COUNT(*) FROM dbo.Orders) AS Orders, (SELECT COUNT(*) FROM dbo.OrderItems) AS Items;

-- читання замовлення з позиціями
DECLARE @Id BIGINT = (SELECT MIN(Id) FROM dbo.Orders);
EXEC dbo.usp_GetOrderWithItems @OrderId = @Id;

-- недопустимий перехід статусу: очікується керована помилка 50002
DECLARE @Id2 BIGINT = (SELECT MIN(Id) FROM dbo.Orders);
EXEC dbo.usp_ChangeOrderStatus @OrderId = @Id2, @NewStatus = 'Shipped';

-- пошук і пагінація (CatalogDb): друга сторінка по 2 товари
SELECT Id, Name, Price FROM dbo.Products
WHERE Name LIKE N'%тест%' ORDER BY Name OFFSET 2 ROWS FETCH NEXT 2 ROWS ONLY;
```

```bash
# MongoDB: кількість документів і різні набори полів у reviews
docker exec mongo1 mongosh ReviewsDb --quiet --eval "printjson({reviews: db.reviews.countDocuments(), sessions: db.sessionResults.countDocuments()})"
docker exec mongo1 mongosh ReviewsDb --quiet --eval "db.reviews.find({}, {rating:1, photos:1, pros:1, device:1}).forEach(printjson)"
```

## Довідка про коди помилок процедур
| Номер | Значення | HTTP (Лаб. 02) |
|---|---|---|
| 50001 | не знайдено | 404 |
| 50002 | конфлікт бізнес-правила (недопустимий перехід) | 409 |
| 50003 | некоректні вхідні дані | 400 |


---

# Лаб. 02: мікросервіс Orders (ADO.NET + Dapper, тришарова архітектура)

## Схема рішення
```
Platform.slnx
├── Platform.AppHost/            оркестрація Aspire: SQL Server + Orders.Api
└── Services/Orders/
    ├── Orders.Api/              Web/API: контролери, Program.cs, обробка помилок (ProblemDetails), Swagger
    ├── Orders.Bll/              сервіси, DTO, профілі AutoMapper, бізнес-валідація
    ├── Orders.Dal/              репозиторії (ADO.NET + Dapper), Unit of Work
    └── Orders.Domain/           моделі та доменні винятки
```
Залежності йдуть в один бік: **Api -> Bll -> Dal**; `Domain` підключають Dal і Bll.
Контролер не звертається до репозиторіїв напряму, жоден SQL-запит не виходить за межі `Orders.Dal`.

| Шар | Що містить |
|---|---|
| Orders.Dal | `CustomerRepository` (чистий ADO.NET, без Generic Repository); `OrderRepository`, `ProductRepository` (Dapper, спільна база `BaseDapperRepository<T>`); `UnitOfWork` (одне з'єднання й одна транзакція на запит); `SqlErrors` (SqlException -> доменні винятки) |
| Orders.Bll | `OrderService` (транзакційні операції створення і підтвердження замовлення), `CustomerService`, `ProductService` |
| Orders.Api | `OrdersController`, `CustomersController`, `ProductsController`; `DomainExceptionHandler` |

Бізнес-операції, що змінюють кілька таблиць в одній транзакції:
- **створення замовлення**: `Orders` + `OrderDetails` + `OrderItems`; назва й ціна товару копіюються в позицію як знімок (політика дублювання з Лаб. 01);
- **підтвердження замовлення**: статус -> `StockConfirmed` (процедура `usp_ConfirmOrder`) і зменшення залишку товарів; якщо товару не вистачає, відкочується все, зокрема й статус.

Рівень ізоляції транзакцій: `ReadCommitted` (дивиться `UnitOfWork`). Вищий рівень зменшує аномалії читання, але збільшує блокування й ризик взаємних блокувань. Від втрати оновлень залишок захищають атомарний `UPDATE ... WHERE Stock >= @Quantity` і `UPDLOCK` у процедурах.

## Запуск через Aspire (Лаб 02)

Потрібні: .NET 9 SDK, Docker Desktop (запущений), SSMS або Visual Studio для застосування SQL-скриптів.

1. Задати пароль SQL Server для Aspire (зберігається в user-secrets, у репозиторії його немає):
```bash
   dotnet user-secrets set "Parameters:sql-password" "<ваш_пароль>" --project Platform.AppHost
```
   Пароль має відповідати вимогам SQL Server (великі й малі літери, цифри, спецсимвол).
2. Запустити все однією командою:
```bash
   dotnet run --project Platform.AppHost
```
   У консолі буде посилання на Aspire Dashboard. У ньому ресурси `sql`, `OrdersDb`, `orders-api` мають статус **Running**.
3. База створюється порожньою. Підключитися до `127.0.0.1,14330` (логін `sa`, пароль з кроку 1, Trust Server Certificate) і виконати **по черзі** `db/p1/schema.sql`, `db/p1/procedures.sql`, `db/p1/seed.sql`.
   > Якщо `localhost,14330` не підключається, використайте `127.0.0.1,14330` (`localhost` може перетворюватися на IPv6-адресу).
4. Адресу API взяти в Dashboard (колонка Endpoints ресурсу `orders-api`), Swagger відкривається за `<адреса>/swagger`.

Рядок підключення: `ConnectionStrings:OrdersDb` в `appsettings.json` лише заглушка. Aspire передає справжній через змінну оточення `ConnectionStrings__OrdersDb` (ім'я ресурсу бази в AppHost збігається з ім'ям рядка підключення), а для запуску API окремо без Aspire його можна задати через `dotnet user-secrets` або змінну оточення.

## Ендпоінти й статуси відповідей
| Метод і шлях | Успіх | Помилки |
|---|---|---|
| `GET /api/customers`, `GET /api/customers/{id}` | 200 | 404 |
| `POST /api/customers` | 201 + Location | 400 (валідація), 409 (дубль Email) |
| `DELETE /api/customers/{id}` | 204 | 404, 409 (є незавершені замовлення) |
| `GET /api/products`, `GET /api/products/{id}` | 200 | 404 |
| `GET /api/orders?customerId=1`, `GET /api/orders/{id}` | 200 | 404 |
| `POST /api/orders` | 201 + Location | 400, 404 (клієнт чи товар), 409 (не вистачає товару) |
| `POST /api/orders/{id}/confirm` | 204 | 404, 409 (недопустимий перехід, брак товару) |
| `PUT /api/orders/{id}/status` | 204 | 400, 404, 409 (недопустимий перехід) |
| `DELETE /api/orders/{id}` | 204 | 404, 409 |

Усі помилки повертаються у форматі **ProblemDetails**. Один глобальний обробник (`DomainExceptionHandler`) перетворює винятки: `NotFoundException` -> 404, `BusinessConflictException` -> 409, `ValidationException` -> 400, решта -> 500. Помилки процедур БД проходять ланцюжок: `THROW 5000x` -> `SqlException` -> DAL -> доменний виняток -> HTTP-статус.

## Приклади запитів
Замініть `{BASE}` на адресу API з Dashboard (наприклад `http://localhost:5044`). Приклади для bash / Git Bash / Postman; у PowerShell використовуйте `curl.exe` або Swagger.

```bash
# список товарів (200)
curl {BASE}/api/products

# створити замовлення (201 + Location)
curl -X POST {BASE}/api/orders -H "Content-Type: application/json" \
  -d '{"customerId":1,"deliveryEmail":"olena.koval@example.com","items":[{"productId":1,"units":2}]}'

# підтвердити замовлення (204); повторний виклик дає 409
curl -X POST {BASE}/api/orders/2/confirm

# неіснуюче замовлення (404, ProblemDetails)
curl {BASE}/api/orders/99999

# некоректне тіло (400): порожній список позицій
curl -X POST {BASE}/api/orders -H "Content-Type: application/json" \
  -d '{"customerId":1,"deliveryEmail":"a@b.com","items":[]}'
```

## Перевірка відкату транзакції
Товар №4 має залишок 50. Створити два замовлення на 50 одиниць цього товару (обидва створяться), підтвердити перше (204, залишок 0), потім друге: відповідь **409** (`Not enough stock`), а замовлення лишається в статусі `Submitted`. Це доводить, що зміна статусу, виконана процедурою, відкотилася разом із невдалим зменшенням залишку.

---

# Лаб. 03: мікросервіс Catalog (Entity Framework Core)

## Схема рішення
```
Services/Catalog/
├── Catalog.Api/      контролери, Program.cs, DomainExceptionHandler, Swagger
├── Catalog.Bll/      сервіси, DTO, профілі AutoMapper, валідатори FluentValidation
├── Catalog.Dal/      AppDbContext, Configurations/ (Fluent API), Migrations/, репозиторії,
│                     Unit of Work, Specifications/, DbInitializer (сідінг)
└── Catalog.Domain/   сутності з бізнес-правилами, доменні винятки, ProductQuery
```
Залежності: **Api -> Bll -> Dal**, `Domain` підключають Dal і Bll. Контролери не звертаються до `DbContext`.

| Шар | Що містить |
|---|---|
| Domain | `Product`, `Category`, `ProductDetail` (1:1), `ProductImage` (1:N), `ProductCategory` (M:N із полем `AddedAt`). Правила всередині сутностей: приватні сеттери, перевірки в конструкторі, зміна стану лише методами (`ChangePrice`, `RemoveStock`, `AddStock`) |
| Dal | окремі `IEntityTypeConfiguration<T>`, підключені в `OnModelCreating` через `ApplyConfigurationsFromAssembly`; Async Generic Repository; три специфічні репозиторії; Unit of Work з єдиним `SaveChangesAsync`; специфікації (Ardalis.Specification) |
| Bll | `ProductService`, `CategoryService`, DTO, AutoMapper, `CreateProductDtoValidator`, `ProductQueryValidator` |

Три специфічні репозиторії:
- **Eager Loading** (`ProductRepository.GetWithDetailsAsync`): `Include/ThenInclude`, **1 SQL-запит**;
- **Explicit Loading** (`CategoryRepository.GetWithProductsAsync`): `Entry(...).Collection(...).LoadAsync()`, **2 SQL-запити**;
- **LINQ через проміжну сутність M:N** (`ProductCategoryRepository.GetRecentlyAddedAsync`): фільтр за полем `AddedAt`, 1 запит.

## Запуск
Потрібні: .NET 9 SDK, SQL Server (контейнер `sql1` з Лаб. 01-02 на порту 1433), інструмент `dotnet-ef` 9.x.

1. Задати рядок підключення (пароль зберігається в user-secrets, у репозиторії його немає):
```bash
   dotnet user-secrets set "ConnectionStrings:CatalogDb" "Server=127.0.0.1,1433;Database=CatalogDb;User Id=sa;Password=<ваш_пароль>;TrustServerCertificate=True" --project Services/Catalog/Catalog.Api
```
2. Запустити сервіс:
```bash
   dotnet run --project Services/Catalog/Catalog.Api
```
   У середовищі Development при старті автоматично застосовуються міграції (`MigrateAsync`) і виконується ідемпотентний сідінг (у production сідінг вимкнено). Swagger: `<адреса>/swagger`.

## Міграції
```bash
dotnet tool install --global dotnet-ef --version "9.*"
dotnet ef migrations add InitialCreate --project Services/Catalog/Catalog.Dal --startup-project Services/Catalog/Catalog.Api
dotnet ef database update --project Services/Catalog/Catalog.Dal --startup-project Services/Catalog/Catalog.Api
dotnet ef migrations script --project Services/Catalog/Catalog.Dal --startup-project Services/Catalog/Catalog.Api
```
Початкова міграція лежить у `Services/Catalog/Catalog.Dal/Migrations/`, згенерований SQL-скрипт: `Migrations/InitialCreate.sql`.
Переглянути все, що створила міграція: таблиці, складений ключ `ProductCategories`, `CHECK` на ціну й залишок, унікальний індекс `Sku`.

## Сідінг
- **Статичний** (`HasData` у `CategoryConfiguration`): п'ять категорій із фіксованими Id.
- **Програмний** (`DbInitializer`, виконується після `MigrateAsync`): п'ять товарів із деталями, зображеннями й категоріями. Повторний запуск нічого не додає, бо перед вставкою перевіряється наявність товарів. Id товарів 1-5 збігаються з локальною копією довідника в БД Замовлень (Лаб. 01-02).

## Ендпоінти й статуси відповідей
| Метод і шлях | Успіх | Помилки |
|---|---|---|
| `GET /api/products` (пагінація, фільтри, сортування) | 200 | 400 |
| `GET /api/products/{id}` (Eager Loading) | 200 | 404 |
| `GET /api/products/low-stock?threshold=10` (друга специфікація) | 200 | 400 |
| `POST /api/products` | 201 + Location | 400 (валідація), 409 (дубль SKU) |
| `PUT /api/products/{id}/price` | 204 | 400, 404 |
| `POST /api/products/{id}/stock/add`, `/stock/remove` | 204 | 400, 404, 409 (нестача товару) |
| `PUT /api/products/{id}/categories/{categoryId}` | 204 | 404, 409 (дубль) |
| `DELETE /api/products/{id}` | 204 | 404 |
| `GET /api/categories`, `GET /api/categories/{id}` (Explicit Loading) | 200 | 404 |
| `GET /api/categories/recent-products?name=Увага&since=2020-01-01` (M:N) | 200 | 400 |

Помилки повертаються у форматі **ProblemDetails** (`ValidationProblemDetails` для 400 із валідації). Глобальний обробник `DomainExceptionHandler`: `EntityNotFoundException` -> 404, `ValidationException` -> 400, `ConflictException` і `DomainException` -> 409, решта -> 500 (деталі лише в Development). Кожен виняток логується один раз.

## Пагінація, фільтрація, сортування
`GET /api/products?page=1&pageSize=10&search=тест&minPrice=50&maxPrice=200&categoryId=1&sortBy=price&sortDir=desc`

| Параметр | Опис |
|---|---|
| `page` | номер сторінки від 1 (за замовчуванням 1) |
| `pageSize` | розмір сторінки, за замовчуванням 20, максимум 100 |
| `search`, `minPrice`, `maxPrice`, `categoryId` | фільтри |
| `sortBy` | `name` (за замовчуванням), `price`, `stock` |
| `sortDir` | `asc` (за замовчуванням) або `desc` |

Формат відповіді: у **тілі** `items`, `totalCount`, `page`, `pageSize`, `totalPages`, `hasNext`, `hasPrevious`; кількість усіх записів також у заголовку `X-Total-Count`. Фільтрація, сортування, `COUNT` і `OFFSET/FETCH` виконуються в SQL, у пам'ять потрапляє лише одна сторінка. Критерії описує `ProductsFilterSpec`.

## Логування
Serilog зі структурованими властивостями (`CorrelationId`, шлях запиту), вивід у консоль. Рівень логування SQL-команд EF Core: `Information`, тому в консолі видно, що Eager Loading виконує один запит, а Explicit Loading два (`Executed DbCommand`).
Налаштування: `appsettings.json` (секція `Serilog`) і код у `Program.cs`.

## Приклади запитів
```bash
# друга сторінка по 2 товари
curl "{BASE}/api/products?page=2&pageSize=2"

# фільтр за ціною й сортування
curl "{BASE}/api/products?minPrice=100&maxPrice=180&sortBy=price&sortDir=desc"

# створення товару (201); повтор із тим самим sku дає 409
curl -X POST {BASE}/api/products -H "Content-Type: application/json" \
  -d '{"name":"Тест Векслера","sku":"WAIS-01","price":250}'

# помилка валідації (400 з переліком помилок по полях)
curl -X POST {BASE}/api/products -H "Content-Type: application/json" \
  -d '{"name":"","sku":"bad sku","price":-5}'
```

---

# Лаб. 04: мікросервіс Reviews (Clean Architecture, CQRS, MongoDB)

## Схема шарів
```
Services/Reviews/
├── Reviews.Domain/           BaseEntity, ValueObject, сутності, винятки, IReviewRepository
├── Reviews.Application/      команди й запити (MediatR), behaviors, валідатори, IReviewQueries
├── Reviews.Infrastructure/   MongoDbContext, BsonClassMap, репозиторій, запити, сідінг, індекси
└── Reviews.WebApi/           контролери (ISender), Program.cs, обробка помилок
```
Залежності йдуть лише досередини: **WebApi -> Application, Infrastructure**; **Infrastructure -> Application, Domain**; **Application -> Domain**; **Domain не посилається ні на що**.

| Шар | Що містить |
|---|---|
| Domain | `Review` і `Comment` із `BaseEntity` (`Id` ObjectId, `CreatedAt`, `UpdatedAt`, `Version`); Value Objects `Author` і `Rating` на базі `ValueObject`; доменні винятки; інтерфейс `IReviewRepository` |
| Application | команди `CreateReview`, `UpdateReview`, `DeleteReview`, `AddComment`; запити `GetReviewById`, `GetReviewsList` через `IReviewQueries`; `ValidationBehavior`, `LoggingBehavior`, `PerformanceBehavior`; валідатори FluentValidation; профіль AutoMapper |
| Infrastructure | `MongoDbContext` через `IOptions<MongoDbOptions>`; `BsonClassMap` без атрибутів у домені; `ReviewRepository` з оптимістичною конкурентністю; `ReviewQueries` з проєкцією в DTO; `IDataSeeder`; `IndexCreationService` |
| WebApi | `ReviewsController` лише через `ISender`; `DomainExceptionHandler` (ProblemDetails, MongoDB-винятки); Swagger; health check |

Порядок виконання pipeline: **Logging -> Validation -> Performance -> обробник**. Невалідна команда до обробника не доходить.

## Запуск
Потрібні .NET 9 SDK і MongoDB (Docker).
```bash
docker run -d --name mongo1 -p 27017:27017 mongo:7
dotnet run --project Services/Reviews/Reviews.WebApi
```
Swagger: `<адреса із консолі>/swagger`. При старті автоматично створюються індекси й (у Development) виконується ідемпотентний сідінг.

Конфігурація: секція `MongoDb` в `appsettings.json` (рядок підключення, `DatabaseName`, `SeedOnStartup`) з перевизначенням у `appsettings.{Environment}.json`. Параметри пулу й таймаутів задані в рядку підключення (`maxPoolSize`, `serverSelectionTimeoutMS`). У Production сідінг вимкнено, а рядок підключення задається змінною оточення `MongoDb__ConnectionString`.

> Структура документів у Лаб. 04 відрізняється від Лаб. 01: `author.userId` тут рядок, а колекцію й індекси створює сам застосунок (без `$jsonSchema`). Тому перед першим запуском стару базу з Лаб. 01 видаляють: `docker exec mongo1 mongosh --eval "db.getSiblingDB('ReviewsDb').dropDatabase()"`.

## Ендпоінти й статуси
| Метод і шлях | Успіх | Помилки |
|---|---|---|
| `GET /api/reviews?productId=1&page=1&pageSize=20` | 200 | 400 |
| `GET /api/reviews/{id}` (версія в `ETag`) | 200 | 400 (не ObjectId), 404 |
| `POST /api/reviews` | 201 + Location | 400 (валідація, дубль відгуку), 409 |
| `PUT /api/reviews/{id}` (заголовок `If-Match` необов'язковий) | 200 | 400, 404, 409 (конфлікт версій) |
| `DELETE /api/reviews/{id}` | 204 | 400, 404 |
| `POST /api/reviews/{id}/comments` | 204 | 400, 404, 409 |
| `GET /health` | 200 | 503 |

Помилки: `NotFoundException` -> 404, `ValidationException` -> 400 (з помилками по полях), `DomainException` -> 400, `ConflictException` -> 409, `MongoWriteException` з `DuplicateKey` -> 409, `MongoConnectionException` і `TimeoutException` -> 503, решта -> 500.

## Індекси
| Індекс | Призначення |
|---|---|
| `ix_product_created` (`productId` asc, `createdAt` desc) | останні відгуки про товар |
| `ix_text` (text по `text`) | повнотекстовий пошук |
| `ux_product_author` (`productId`, `author.userId`, unique) | один відгук від автора на товар |

Перевірка: `db.reviews.getIndexes()`.

## Приклади запитів
```bash
# створення відгуку (201); rating: 10 дає 400 ще до виклику обробника
curl -X POST {BASE}/api/reviews -H "Content-Type: application/json" \
  -d '{"productId":4,"productName":"Просторове мислення: ментальне обертання","authorId":"u-9","authorName":"Іван","rating":4,"text":"Корисно"}'

# оновлення з перевіркою версії (повторний запит із тим самим If-Match дає 409)
curl -X PUT {BASE}/api/reviews/{id} -H "Content-Type: application/json" -H 'If-Match: "0"' \
  -d '{"rating":5,"text":"Оновлено"}'
```



---

# Лаб 05: Платформа на .NET Aspire

Розділи вище описують запуск окремих сервісів (Лаб. 02–04). Цей розділ описує **запуск усієї платформи однією командою**: три мікросервіси, бази даних, API Gateway, Aggregator і спостережуваність.

## Архітектура

| Компонент | Проєкт | Призначення |
|---|---|---|
| AppHost | `Platform.AppHost` | Оркестрація: запускає контейнери SQL Server і MongoDB, сервіси, Aggregator і Gateway; передає рядки підключення та налаштовує service discovery. Бізнес-логіки не містить |
| ServiceDefaults | `Platform.ServiceDefaults` | Спільні налаштування, які підключає кожен сервіс: Serilog (структуровані логи), OpenTelemetry (HTTP, SQL, MongoDB, експорт OTLP), CorrelationId (middleware + DelegatingHandler), health checks, resilience |
| API Gateway | `Platform.ApiGateway` | Єдина точка входу на YARP. Маршрутизує запити за логічними іменами сервісів, генерує CorrelationId, логує маршрутизацію |
| Aggregator | `Platform.Aggregator` | Паралельно (`Task.WhenAll`) опитує кілька сервісів і збирає одну відповідь; при недоступності сервісу повертає часткову відповідь із переліком попереджень |
| Мікросервіси | `Services/Orders`, `Services/Catalog`, `Services/Reviews` | Замовлення (SQL Server, ADO.NET/Dapper), Каталог (SQL Server, EF Core), Відгуки (MongoDB, Clean Architecture + CQRS) |

Залежності односпрямовані: AppHost посилається на всі проєкти, а сервіси, Gateway та Aggregator лише на ServiceDefaults. Сервіси знаходять один одного за логічними іменами (`orders-api`, `catalog-api`, `reviews-api`, `aggregator`), а не за портами.

Порядок запуску задається в AppHost: бази даних → сервіси → Aggregator → Gateway.

## Запуск

Вимоги: .NET 9 SDK, Docker Desktop (має бути запущений до старту).

1. Пароль SQL Server задається секретом AppHost (одноразово):

   ```bash
   cd Platform.AppHost
   dotnet user-secrets set "Parameters:sql-password" "<ваш пароль>"
   cd ..
   ```

2. Запуск усієї платформи:

   ```bash
   dotnet run --project Platform.AppHost
   ```

   У браузері відкриється **Aspire Dashboard**. Усі ресурси (`sql`, `mongo`, `orders-api`, `catalog-api`, `reviews-api`, `aggregator`, `gateway`) мають отримати статус **Running**. Контейнери баз використовують постійні volume, тож дані зберігаються між перезапусками.

3. Розгортання БД Замовлень (лише при першому запуску): після старту SQL Server виконайте скрипти `db/p1/schema.sql`, `procedures.sql`, `seed.sql` на `127.0.0.1,14330` (логін `sa`, пароль із кроку 1). Каталог (EF Core міграції) і Відгуки (MongoDB) ініціалізуються самі при старті.

## Gateway та Aggregator

Єдина точка входу для клієнтів: **http://localhost:5000**

| Маршрут через Gateway | Куди проксується |
|---|---|
| `/api/orders/…`, `/api/customers/…` | `orders-api` |
| `/api/products/…`, `/api/categories/…` | `catalog-api` |
| `/api/reviews/…` | `reviews-api` |
| `/api/aggregator/…` | `aggregator` (пріоритетний маршрут) |

Приклади запитів (у Windows PowerShell використовуйте `curl.exe`):

```bash
# звичайні запити через Gateway
curl -i http://localhost:5000/api/orders/1
curl -i "http://localhost:5000/api/products?page=1"
curl -i "http://localhost:5000/api/reviews?productId=1"

# агрегований запит: замовлення + товари + відгуки в одній відповіді
curl -i http://localhost:5000/api/aggregator/orders/1/full
```

Відповідь агрегувального ендпоінта містить `order`, `products`, `reviews` і `warnings`. Якщо якийсь із сервісів недоступний, замість помилки 500 приходить часткова відповідь: доступні дані плюс попередження, наприклад `"reviews-api is unavailable"`. Якщо недоступний `orders-api`, відповідь буде `503`, бо без замовлення агрегувати нічого; відсутнє замовлення дає `404`.

## CorrelationId і спостережуваність

- Кожна відповідь містить заголовок `X-Correlation-Id`. Gateway генерує його на вході (або зберігає значення клієнта), і він передається далі через усі сервіси та HTTP-виклики Aggregator.
- **Aspire Dashboard → Structured logs**: фільтр `CorrelationId = <значення з відповіді>` показує логи всіх сервісів ланцюжка. Кожен запис містить `ServiceName`, `Environment`, `MachineName`, `TraceId`, `SpanId`.
- **Aspire Dashboard → Traces**: повний шлях `gateway → aggregator → сервіси → SQL/MongoDB` в одному trace; виклики до `catalog-api` і `reviews-api` стартують паралельно.
- Логи Gateway містять рішення про маршрутизацію (маршрут, метод, шлях, статус, тривалість, помилки недоступних сервісів).

## Перевірка часткової відповіді

1. У Dashboard зупиніть ресурс `reviews-api` (Stop).
2. Виконайте `curl -i http://localhost:5000/api/aggregator/orders/1/full`: відповідь `200`, у `warnings` є `reviews-api is unavailable`, замовлення й товари на місці.
3. Запустіть `reviews-api` знову: відповідь знову повна, `warnings` порожній.

## Типові проблеми

| Симптом | Причина і рішення |
|---|---|
| Сервіси не стартують, контейнери не з'являються | Docker Desktop не запущений: запустіть його і перезапустіть AppHost |
| `Login failed for user 'sa'` | Змінили `sql-password`, а volume SQL Server зберігає старий пароль: видаліть volume контейнера `sql` і запустіть знову |
| `OrdersDb` порожня, замовлення не знаходяться | Не виконано скрипти `db/p1` (див. крок 3 запуску) |
| Порт 5000 зайнятий | Звільніть порт або змініть `port: 5000` для Gateway в `Platform.AppHost/Program.cs` |



---

# Лаб. 06: Кешування та gRPC

Лаб. 06 розширює платформу з Лаб. 05: кеш у пам'яті та в Redis, gRPC-контракти в мікросервісах, Aggregator на gRPC.

## Redis

Redis запускається з AppHost автоматично (контейнер `redis` із постійним volume). Сервіси `orders-api`, `catalog-api`, `reviews-api` і `aggregator` отримують рядок підключення `ConnectionStrings:redis` від Aspire. Окремо налаштовувати нічого не треба: достатньо `dotnet run --project Platform.AppHost` (Docker Desktop запущений).

## Кешування

Схема читання: **L1 (IMemoryCache) → L2 (Redis) → БД**, проміжні рівні заповнюються на зворотному шляху; L1 завжди коротший за L2. Якщо Redis недоступний, сервіси працюють без нього (повільніше). Метрики `cache.hits` / `cache.misses` (тег `cache.level`) видно в Aspire Dashboard → Metrics.

| Сервіс | Що кешується | L1 (пам'ять) | L2 (Redis) | Інвалідація |
|---|---|---|---|---|
| Catalog | картка товару `catalog:product:{id}`, список категорій | 1 хв / 5 хв | 10 хв / 1 год | після зміни ціни, залишків, категорій, видалення |
| Orders | замовлення `orders:order:{id}` (персональні дані, тому TTL короткі) | 15 с | 60 с | після підтвердження, зміни статусу, видалення |
| Reviews | відгук `reviews:review:{id}`, сторінки списків `reviews:list:…` | 1 хв / 30 с | 10 хв / 5 хв | після створення, зміни, видалення відгуку і коментаря (списки: ключ-версія) |
| Aggregator | агрегована картка замовлення `aggregator:order:{id}:full` | немає | 30 с | лише TTL; часткові відповіді не кешуються |

## gRPC

Контракти лежать у `Services/Catalog/Catalog.Api/Protos`, `Services/Orders/Orders.Api/Protos`, `Services/Reviews/Reviews.WebApi/Protos`. gRPC-сервіси (`*/Grpc/`) є адаптерами над бізнес-логікою і повертають `RpcException` з кодами `NotFound` / `InvalidArgument`. Aggregator викликає сервіси через gRPC (типізовані клієнти із service discovery, паралельні виклики через `Task.WhenAll`, часткові відмови як попередження). Для порівняння лишено HTTP-версію з Лаб. 05:

```bash
curl -i http://localhost:5000/api/aggregator/orders/1/full                  # gRPC + кеш Redis (30 с)
curl -i "http://localhost:5000/api/aggregator/orders/1/full?noCache=true"   # gRPC без кешу
curl -i http://localhost:5000/api/aggregator/orders/1/full/http             # HTTP (Лаб. 05)
```

gRPC працює поверх HTTP/2 на HTTPS-ендпоінтах сервісів: потрібен довірений dev-сертифікат (`dotnet dev-certs https --trust`), AppHost запускається з профілем `https`.

## Порівняння HTTP та gRPC

| Шлях | Середня тривалість агрегувального запиту |
|---|---|
| HTTP (Лаб. 05) | _заповнити за результатами вимірювань_ |
| gRPC | _заповнити за результатами вимірювань_ |

## Таблиця дублювання (доповнення)

| Що дублюється | Де | Чому |
|---|---|---|
| `TwoLevelCache`, `ICacheMetrics`, `CachePolicy`, `CacheKeys` | `Catalog.Bll/Caching`, `Orders.Bll/Caching`, `Reviews.Application/Caching` | сервіси незалежні, спільної бібліотеки між ними немає |
| `CacheMetrics` (Meter `<Сервіс>.Cache`) | `Catalog.Api/Metrics`, `Orders.Api/Metrics`, `Reviews.WebApi/Metrics` | те саме |
| gRPC-адаптери та `.proto` | `Catalog.Api`, `Orders.Api`, `Reviews.WebApi` | кожен сервіс володіє власним контрактом; Aggregator посилається на ті самі файли |