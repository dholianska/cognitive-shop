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

## Запуск через Aspire

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