namespace MindMapper
{
    public interface IMapper
    {
        TDestination Map<TDestination>(object source) where TDestination : new();
        List<TDestination> Map<TDestination>(IEnumerable<object> sources) where TDestination : new();
        List<TDestination> Map<TSource, TDestination>(IEnumerable<TSource> sources) where TDestination : new();
        TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    }
}
