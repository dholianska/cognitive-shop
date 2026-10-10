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