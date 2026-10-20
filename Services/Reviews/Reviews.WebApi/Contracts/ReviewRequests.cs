namespace Reviews.WebApi.Contracts;

/// <summary>Тіло PUT /api/reviews/{id}. Очікувана версія передається заголовком If-Match.</summary>
public record UpdateReviewRequest(int Rating, string Text);

/// <summary>Тіло POST /api/reviews/{id}/comments.</summary>
public record AddCommentRequest(string AuthorId, string AuthorName, string Text);