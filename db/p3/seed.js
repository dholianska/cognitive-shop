/* =====================================================================
   Проєкт №3 - початкові дані БД Відгуків. Ідемпотентний:
   документ із таким _id вставляється лише якщо його ще немає.
   Запуск: mongosh --file db/p3/seed.js
   ===================================================================== */
db = db.getSiblingDB("ReviewsDb");

function seed(collection, doc) {
    const { _id, ...rest } = doc;
    db.getCollection(collection).updateOne({ _id: _id }, { $setOnInsert: rest }, { upsert: true });
}

// фіксовані _id, щоб документи могли посилатися один на одного
const DISCUSSION_1 = ObjectId("66f0c3a00000000000000001");

/* ---------- reviews: різні набори полів в одній колекції ---------- */

// повний відгук: фото, вбудовані коментарі, посилання на обговорення
seed("reviews", {
    _id: ObjectId("66f0c2a00000000000000001"),
    productId: NumberInt(1),
    productName: "Тренажер уваги: тест Струпа",          // денормалізовано
    author: { userId: NumberInt(1), displayName: "Олена" },
    rating: NumberInt(5),
    text: "Чудовий тренажер, помітно покращилась концентрація",
    photos: ["img1.jpg", "img2.jpg"],
    comments: [
        { author: "Андрій", text: "Згоден, користуюсь другий тиждень", createdAt: ISODate("2026-09-02T12:00:00Z") }
    ],
    discussionId: DISCUSSION_1,                           // reference
    createdAt: ISODate("2026-09-01T10:00:00Z")
});

// відгук з іншими полями: плюси й мінуси, позначка перевіреної покупки
seed("reviews", {
    _id: ObjectId("66f0c2a00000000000000002"),
    productId: NumberInt(2),
    productName: "N-back: тренування робочої пам'яті",
    author: { userId: NumberInt(2), displayName: "Андрій" },
    rating: NumberInt(4),
    text: "Добре для робочої пам'яті, але хотілося б більше рівнів",
    verifiedPurchase: true,
    pros: ["Адаптивна складність", "Зрозумілий інтерфейс"],
    cons: ["Мало рівнів"],
    createdAt: ISODate("2026-09-05T15:30:00Z")
});

// мінімальний відгук: лише обов'язкові поля
seed("reviews", {
    _id: ObjectId("66f0c2a00000000000000003"),
    productId: NumberInt(1),
    productName: "Тренажер уваги: тест Струпа",
    author: { userId: NumberInt(3), displayName: "Марія" },
    rating: NumberInt(3),
    text: "Нормально, але одноманітно",
    createdAt: ISODate("2026-09-10T09:15:00Z")
});

// відгук з метаданими пристрою й тегами
seed("reviews", {
    _id: ObjectId("66f0c2a00000000000000004"),
    productId: NumberInt(3),
    productName: "Тест часу реакції",
    author: { userId: NumberInt(1), displayName: "Олена" },
    rating: NumberInt(5),
    text: "Швидкий тест, зручно проходити з телефона",
    device: "mobile",
    tags: ["реакція", "швидко"],
    createdAt: ISODate("2026-09-12T18:45:00Z")
});

/* ---------- discussions: гібрид (вбудований прев'ю + окрема колекція) ---------- */
seed("discussions", {
    _id: DISCUSSION_1,
    productId: NumberInt(1),
    productName: "Тренажер уваги: тест Струпа",
    title: "Як краще тренуватися з тестом Струпа?",
    startedBy: { userId: NumberInt(1), displayName: "Олена" },
    messageCount: NumberInt(3),
    lastMessages: [                                       // вбудований прев'ю останніх повідомлень
        { author: "Андрій", text: "Я проходжу раз на день по 5 хвилин", createdAt: ISODate("2026-09-03T09:00:00Z") },
        { author: "Марія", text: "Спробую так само, дякую", createdAt: ISODate("2026-09-03T11:20:00Z") }
    ],
    createdAt: ISODate("2026-09-02T08:00:00Z")
});

/* ---------- discussionMessages: повні повідомлення, reference на обговорення ---------- */
seed("discussionMessages", {
    _id: ObjectId("66f0c4a00000000000000001"),
    discussionId: DISCUSSION_1,
    author: { userId: NumberInt(1), displayName: "Олена" },
    text: "Підкажіть, як краще тренуватися: коротко щодня чи довго раз на тиждень?",
    createdAt: ISODate("2026-09-02T08:00:00Z")
});
seed("discussionMessages", {
    _id: ObjectId("66f0c4a00000000000000002"),
    discussionId: DISCUSSION_1,
    author: { userId: NumberInt(2), displayName: "Андрій" },
    text: "Я проходжу раз на день по 5 хвилин",
    createdAt: ISODate("2026-09-03T09:00:00Z")
});
seed("discussionMessages", {
    _id: ObjectId("66f0c4a00000000000000003"),
    discussionId: DISCUSSION_1,
    author: { userId: NumberInt(3), displayName: "Марія" },
    text: "Спробую так само, дякую",
    createdAt: ISODate("2026-09-03T11:20:00Z")
});

/* ---------- sessionResults: у різних вправ різні метрики ---------- */

// тест Струпа
seed("sessionResults", {
    _id: ObjectId("66f0c5a00000000000000001"),
    userId: NumberInt(1), productId: NumberInt(1),
    startedAt: ISODate("2026-09-20T08:00:00Z"),
    durationMs: NumberInt(300000), score: NumberInt(82),
    metrics: { meanReactionMs: 640, errors: 4, congruentMeanMs: 590, incongruentMeanMs: 710 }
});

// N-back: інші метрики
seed("sessionResults", {
    _id: ObjectId("66f0c5a00000000000000002"),
    userId: NumberInt(2), productId: NumberInt(2),
    startedAt: ISODate("2026-09-21T19:30:00Z"),
    durationMs: NumberInt(420000), score: NumberInt(75),
    metrics: { n: 2, hits: 18, misses: 4, falseAlarms: 3 }
});

// фокус-сесія: самооцінка концентрації до й після
seed("sessionResults", {
    _id: ObjectId("66f0c5a00000000000000003"),
    userId: NumberInt(1), productId: NumberInt(5),
    startedAt: ISODate("2026-09-22T09:00:00Z"),
    durationMs: NumberInt(1500000),
    metrics: { focusBefore: 5, focusAfter: 8, pomodoroCount: 1 },
    note: "Після сесії легше зосередитися"
});

// тест реакції: масив окремих проб (сирі дані для аналізу)
seed("sessionResults", {
    _id: ObjectId("66f0c5a00000000000000004"),
    userId: NumberInt(3), productId: NumberInt(3),
    startedAt: ISODate("2026-09-23T14:10:00Z"),
    durationMs: NumberInt(60000), score: NumberInt(90),
    metrics: { meanReactionMs: 308 },
    trials: [{ ms: 305, correct: true }, { ms: 322, correct: true }, { ms: 298, correct: true }],
    device: "desktop"
});

print("seed finished");