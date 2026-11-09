namespace Reviews.Application.Caching;

public interface ICacheMetrics
{
    void Hit(string level);
    void Miss(string level);
    void Invalidated();
}