namespace MindMapper
{
    public interface IMappingProfile
    {
        MappingConfig<TSource, TDestination> CreateMap<TSource, TDestination>(Action<MappingConfig<TSource, TDestination>> config = null);
        bool TryGetMapping(Type sourceType, Type destType, out Action<object, object> mappingAction);
        bool TryGetMapping<TSource, TDestination>(out Action<TSource, TDestination> mappingAction);
    }

}
