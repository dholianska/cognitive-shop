using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Reviews.WebApi.Metrics;

/// <summary>Бізнес-метрики відгуків. Singleton: живе протягом усього часу роботи застосунку.</summary>
public sealed class ReviewMetrics
{
    private readonly Counter<long> _created;
    private readonly Counter<long> _updated;
    private readonly Counter<long> _deleted;
    private readonly Counter<long> _comments;
    private readonly Histogram<double> _duration;
    private int _inFlight;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Reviews.WebApi");

        _created = meter.CreateCounter<long>(
            "reviews.created", unit: "{review}", description: "Number of created reviews by rating");
        _updated = meter.CreateCounter<long>(
            "reviews.updated", unit: "{review}", description: "Number of updated reviews");
        _deleted = meter.CreateCounter<long>(
            "reviews.deleted", unit: "{review}", description: "Number of deleted reviews");
        _comments = meter.CreateCounter<long>(
            "reviews.comments_added", unit: "{comment}", description: "Number of added comments");
        _duration = meter.CreateHistogram<double>(
            "reviews.operation.duration", unit: "ms", description: "Duration of review operations");

        meter.CreateObservableGauge(
            "reviews.operations.in_flight", () => Volatile.Read(ref _inFlight),
            unit: "{operation}", description: "Review operations being processed right now");
    }

    /// <param name="rating">оцінка 1..5: п'ять значень, тобто низька кардинальність</param>
    public void ReviewCreated(int rating) => _created.Add(1, Tag("review.rating", rating.ToString()));
    public void ReviewUpdated() => _updated.Add(1);
    public void ReviewDeleted() => _deleted.Add(1);
    public void CommentAdded() => _comments.Add(1);

    public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        Interlocked.Increment(ref _inFlight);
        var sw = Stopwatch.StartNew();
        var success = false;
        try
        {
            var result = await action();
            success = true;
            return result;
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
            _duration.Record(sw.Elapsed.TotalMilliseconds,
                Tag("operation", operation), Tag("status", success ? "success" : "failure"));
        }
    }

    public Task MeasureAsync(string operation, Func<Task> action)
        => MeasureAsync<object?>(operation, async () => { await action(); return null; });

    private static KeyValuePair<string, object?> Tag(string key, object? value) => new(key, value);
}