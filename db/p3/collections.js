/* =====================================================================
   Проєкт №3 - БД Відгуків (MongoDB). Колекції, валідація, індекси.
   Ідемпотентний: повторний запуск не створює дублікатів.
   Запуск: mongosh --file db/p3/collections.js
   ===================================================================== */
db = db.getSiblingDB("ReviewsDb");

// створює колекцію, а якщо вона вже є - оновлює правила валідації
function ensureCollection(name, validator) {
    if (!db.getCollectionNames().includes(name)) {
        db.createCollection(name, { validator: validator });
        print("created: " + name);
    } else {
        db.runCommand({ collMod: name, validator: validator });
        print("exists, validator updated: " + name);
    }
}

/* ---------- reviews: embedding + referencing + гнучка схема ----------
   productId - посилання на Каталог (БЕЗ FK), productName - денормалізована
   копія (оновлюється подіями з Каталогу), author - вбудований об'єкт
   (знімок displayName), comments - вбудований масив, discussionId - reference.
   Додаткові поля (photos, pros, cons, device, tags...) є не в усіх документів. */
ensureCollection("reviews", {
    $jsonSchema: {
        bsonType: "object",
        required: ["productId", "productName", "author", "rating", "createdAt"],
        properties: {
            productId: { bsonType: "int" },
            productName: { bsonType: "string" },
            author: {
                bsonType: "object",
                required: ["userId", "displayName"],
                properties: { userId: { bsonType: "int" }, displayName: { bsonType: "string" } }
            },
            rating: { bsonType: "int", minimum: 1, maximum: 5 },
            comments: { bsonType: "array", items: { bsonType: "object", required: ["author", "text", "createdAt"] } }
        }
    }
});

/* ---------- discussions: ГІБРИДНИЙ підхід ----------
   lastMessages - вбудований прев'ю останніх повідомлень (швидко показати
   у списку), повний список - у колекції discussionMessages. */
ensureCollection("discussions", {
    $jsonSchema: {
        bsonType: "object",
        required: ["productId", "title", "startedBy", "messageCount", "createdAt"],
        properties: {
            productId: { bsonType: "int" },
            messageCount: { bsonType: "int", minimum: 0 },
            lastMessages: { bsonType: "array", maxItems: 3 }
        }
    }
});

/* ---------- discussionMessages: REFERENCING (необмежена підколекція) ---------- */
ensureCollection("discussionMessages", {
    $jsonSchema: {
        bsonType: "object",
        required: ["discussionId", "author", "text", "createdAt"],
        properties: { discussionId: { bsonType: "objectId" } }
    }
});

/* ---------- sessionResults: гнучка схема ----------
   metrics має різну структуру залежно від вправи. */
ensureCollection("sessionResults", {
    $jsonSchema: {
        bsonType: "object",
        required: ["userId", "productId", "startedAt", "durationMs", "metrics"],
        properties: {
            userId: { bsonType: "int" },
            productId: { bsonType: "int" },
            durationMs: { bsonType: "int", minimum: 0 },
            metrics: { bsonType: "object" }
        }
    }
});

/* =====================================================================
   ІНДЕКСИ: кожен створено під конкретний запит
   ===================================================================== */

// Запит: "останні відгуки про товар" (сторінка товару, новіші зверху)
db.reviews.createIndex({ productId: 1, createdAt: -1 }, { name: "IX_reviews_product_created" });

// Запит: "усі відгуки конкретного користувача"
db.reviews.createIndex({ "author.userId": 1 }, { name: "IX_reviews_author" });

// Запит: оновлення productName у всіх відгуках про товар після події з Каталогу
//        (використовує вже створений IX_reviews_product_created)

// Запит: "обговорення про товар"
db.discussions.createIndex({ productId: 1 }, { name: "IX_discussions_product" });

// Запит: "повідомлення обговорення в хронологічному порядку"
db.discussionMessages.createIndex({ discussionId: 1, createdAt: 1 }, { name: "IX_messages_discussion_created" });

// Запит: "історія проходжень користувача, нові зверху"
db.sessionResults.createIndex({ userId: 1, startedAt: -1 }, { name: "IX_sessions_user_started" });

// Запит: "результати проходжень конкретної вправи" (для аналізу)
db.sessionResults.createIndex({ productId: 1, startedAt: -1 }, { name: "IX_sessions_product_started" });

print("collections and indexes are ready");