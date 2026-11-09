namespace Orders.Bll.Caching;

public interface ICacheMetrics
{
    void Hit(string level);
    void Miss(string level);
    void Invalidated();
}