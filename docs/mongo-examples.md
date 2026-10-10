# MongoDB: структура колекцій та приклади (БД ReviewsDb)

| Колекція | Підхід до зв'язків |
|---|---|
| reviews | **embedding**: author, comments; **referencing**: productId (Каталог, без FK), discussionId |
| discussions | **гібрид**: вбудований прев'ю lastMessages + посилання через discussionMessages |
| discussionMessages | **referencing**: повідомлення посилаються на discussionId |
| sessionResults | **гнучка схема**: різна структура metrics для різних вправ |

Денормалізація: `reviews.productName` і `discussions.productName` копіюються з Каталогу
й оновлюються подіями (див. політику дублювання в README).

## Приклад: відгук (embedding + referencing)
```json
{
  "_id": "66f0c2a00000000000000001",
  "productId": 1,
  "productName": "Тренажер уваги: тест Струпа",
  "author": { "userId": 1, "displayName": "Олена" },
  "rating": 5,
  "text": "Чудовий тренажер, помітно покращилась концентрація",
  "photos": ["img1.jpg", "img2.jpg"],
  "comments": [ { "author": "Андрій", "text": "Згоден, користуюсь другий тиждень", "createdAt": "2026-09-02T12:00:00Z" } ],
  "discussionId": "66f0c3a00000000000000001",
  "createdAt": "2026-09-01T10:00:00Z"
}
```

## Приклад: відгук з іншою структурою (гнучка схема)
```json
{
  "productId": 2, "productName": "N-back: тренування робочої пам'яті",
  "author": { "userId": 2, "displayName": "Андрій" }, "rating": 4,
  "text": "Добре для робочої пам'яті, але хотілося б більше рівнів",
  "verifiedPurchase": true, "pros": ["Адаптивна складність"], "cons": ["Мало рівнів"],
  "createdAt": "2026-09-05T15:30:00Z"
}
```

## Приклад: обговорення (гібрид)
```json
{
  "productId": 1, "title": "Як краще тренуватися з тестом Струпа?", "messageCount": 3,
  "lastMessages": [ { "author": "Марія", "text": "Спробую так само, дякую", "createdAt": "2026-09-03T11:20:00Z" } ]
}
```

## Приклад: результат сесії (різні метрики)
```json
{ "userId": 2, "productId": 2, "durationMs": 420000, "score": 75,
  "metrics": { "n": 2, "hits": 18, "misses": 4, "falseAlarms": 3 } }
```

## Запити
```javascript
// останні відгуки про товар 1 (використовує IX_reviews_product_created)
db.reviews.find({ productId: 1 }).sort({ createdAt: -1 })

// оновлення денормалізованої назви після події з Каталогу
db.reviews.updateMany({ productId: 1 }, { $set: { productName: "Нова назва" } })

// aggregation: середній рейтинг і кількість відгуків за товарами
db.reviews.aggregate([
  { $group: { _id: "$productId", avgRating: { $avg: "$rating" }, count: { $sum: 1 } } },
  { $sort: { avgRating: -1 } }
])

// aggregation: середній час реакції за користувачами для тесту Струпа
db.sessionResults.aggregate([
  { $match: { productId: 1 } },
  { $group: { _id: "$userId", meanReactionMs: { $avg: "$metrics.meanReactionMs" } } }
])
```