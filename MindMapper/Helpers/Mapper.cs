namespace MindMapper
{
    public static class Mapper
    {
        private static MappingProfile _profile;

        public static void Initialize(MappingProfile profile)
        {
            _profile = profile;
        }

        public static TDestination Map<TDestination>(object source)
            where TDestination : new()
        {
            if (source == null) return default;

            var sourceType = source.GetType();
            var destinationType = typeof(TDestination);

            if (!_profile.TryGetMapping(sourceType, destinationType, out var mappingFunc))
                throw new InvalidOperationException($"No mappings found from {sourceType} to {destinationType}");

            return (TDestination)mappingFunc(source);
        }

        public static List<TDestination> Map<TSource, TDestination>(IEnumerable<TSource> sources)
        where TDestination : new()
        {
            if (!_profile.TryGetMappingAction<TSource, TDestination>(out var mapAction))
                throw new InvalidOperationException($"No mapping found from {typeof(TSource)} to {typeof(TDestination)}");

            var result = new List<TDestination>();
            foreach (var source in sources)
            {
                var dest = new TDestination();
                mapAction(source, dest);
                result.Add(dest);
            }
            return result;
        }
        public static List<TDestination> Map<TDestination>(IEnumerable<object> sources)
            where TDestination : new()
        {
            var list = new List<TDestination>();
            foreach (var item in sources)
            {
                list.Add(Map<TDestination>(item));
            }
            return list;
        }

        public static TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null || destination == null)
                throw new ArgumentNullException("Source or destination cannot be null.");

            if (!_profile.TryGetMappingAction<TSource, TDestination>(out var mappingAction))
                throw new InvalidOperationException($"No mapping found from {typeof(TSource)} to {typeof(TDestination)}");

            mappingAction(source, destination);
            return destination;
        }
    }

}
