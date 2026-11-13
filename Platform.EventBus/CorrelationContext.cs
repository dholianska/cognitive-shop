using System.Diagnostics;

namespace Platform.EventBus;

public static class CorrelationContext
{
    public const string TagName = "correlation.id";

    /// <summary>CorrelationId поточної операції: шукаємо тег на Activity та її батьках.</summary>
    public static string? Current
    {
        get
        {
            for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
            {
                if (activity.GetTagItem(TagName) is string id && !string.IsNullOrEmpty(id))
                    return id;
            }
            return null;
        }
    }
}