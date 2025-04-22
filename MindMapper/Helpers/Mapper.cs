namespace Mapator
{
    public static class Mapper
    {
        private static CustomMappingProfile _profile;

        public static void Initialize(CustomMappingProfile profile)
        {
            _profile = profile;
        }

        public static TDestination Map<TDestination>(object source)
            where TDestination : new()
        {
            if (source == null) return default;

            var sourceType = source.GetType();
            var destinationType = typeof(TDestination);
            var mappings = _profile.GetMappings(sourceType, destinationType);

            if (mappings == null || mappings.Count == 0)
                throw new InvalidOperationException($"No mappings found from {sourceType} to {destinationType}");

            var destination = new TDestination();
            foreach (var map in mappings)
            {
                map(source, destination);
            }

            return destination;
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

            var key = (typeof(TSource), typeof(TDestination));

            if (!_profile.TryGetMapping(key, out var propertyMappings))
                throw new InvalidOperationException($"No mapping found from {typeof(TSource)} to {typeof(TDestination)}");

            foreach (var map in propertyMappings)
            {
                map(source!, destination!);
            }

            return destination;
        }


    }
}
