namespace MindMapper
{
    public class CustomMappingProfile
    {
        private readonly Dictionary<(Type source, Type destination), List<Action<object, object>>> _mappings
            = new();

        public CustomMappingConfig<TSource, TDestination> CreateMap<TSource, TDestination>(Action<CustomMappingConfig<TSource, TDestination>> config = null)
        {
            var mapConfig = new CustomMappingConfig<TSource, TDestination>(this);

            // Apply user-defined config (set ignore list etc.)
            config?.Invoke(mapConfig);

            // Auto-map forward
            ApplyAutoMap(mapConfig);

            // Register forward mapping
            var key = (typeof(TSource), typeof(TDestination));
            _mappings[key] = mapConfig.PropertyMappings;

            return mapConfig;
        }

        public void CreateReverseMap<TSource, TDestination>(CustomMappingConfig<TSource, TDestination> originalConfig)
        {
            var reverseConfig = new CustomMappingConfig<TDestination, TSource>(this);

            // Copy ignored properties from original config to reverse config
            foreach (var ignoredProp in originalConfig.IgnoredProperties)
            {
                reverseConfig.IgnoredProperties.Add(ignoredProp);
            }

            // Auto-map in reverse direction
            ApplyReverseAutoMap(originalConfig, reverseConfig);

            // Register reverse mapping
            var key = (typeof(TDestination), typeof(TSource));
            _mappings[key] = reverseConfig.PropertyMappings;
        }

        public List<Action<object, object>> GetMappings(Type sourceType, Type destType)
        {
            var key = (sourceType, destType);
            return _mappings.TryGetValue(key, out var mappings) ? mappings : new List<Action<object, object>>();
        }

        private void ApplyAutoMap<TSource, TDestination>(CustomMappingConfig<TSource, TDestination> mapConfig)
        {
            var sourceProps = typeof(TSource).GetProperties();
            var destProps = typeof(TDestination).GetProperties();

            foreach (var destProp in destProps)
            {
                if (mapConfig.IgnoredProperties.Contains(destProp.Name))
                    continue;

                var srcProp = sourceProps.FirstOrDefault(p =>
                    p.Name == destProp.Name && p.CanRead && destProp.CanWrite);

                if (srcProp == null) continue;

                if (srcProp.PropertyType == destProp.PropertyType)
                {
                    mapConfig.PropertyMappings.Add((src, dest) =>
                    {
                        var val = srcProp.GetValue(src);
                        destProp.SetValue(dest, val);
                    });
                }
                else if (srcProp.PropertyType.IsEnum && destProp.PropertyType == typeof(string))
                {
                    mapConfig.PropertyMappings.Add((src, dest) =>
                    {
                        var val = srcProp.GetValue(src);
                        destProp.SetValue(dest, val?.ToString());
                    });
                }
                else if (srcProp.PropertyType == typeof(string) && destProp.PropertyType.IsEnum)
                {
                    mapConfig.PropertyMappings.Add((src, dest) =>
                    {
                        var val = srcProp.GetValue(src) as string;
                        if (Enum.TryParse(destProp.PropertyType, val, out var parsed))
                            destProp.SetValue(dest, parsed);
                    });
                }
            }
        }

        private void ApplyReverseAutoMap<TSource, TDestination>(
        CustomMappingConfig<TSource, TDestination> originalConfig,
        CustomMappingConfig<TDestination, TSource> reverseConfig)
        {
            var sourceProps = typeof(TSource).GetProperties();
            var destProps = typeof(TDestination).GetProperties();

            foreach (var srcProp in sourceProps)
            {
                if (originalConfig.IgnoredProperties.Contains(srcProp.Name))
                    continue;

                var destProp = destProps.FirstOrDefault(p =>
                    p.Name == srcProp.Name && p.CanRead && srcProp.CanWrite);

                if (destProp == null) continue;

                if (destProp.PropertyType == srcProp.PropertyType)
                {
                    reverseConfig.PropertyMappings.Add((src, dest) =>
                    {
                        var val = destProp.GetValue(src);
                        srcProp.SetValue(dest, val);
                    });
                }
                else if (destProp.PropertyType.IsEnum && srcProp.PropertyType == typeof(string))
                {
                    reverseConfig.PropertyMappings.Add((src, dest) =>
                    {
                        var val = destProp.GetValue(src);
                        srcProp.SetValue(dest, val?.ToString());
                    });
                }
                else if (destProp.PropertyType == typeof(string) && srcProp.PropertyType.IsEnum)
                {
                    reverseConfig.PropertyMappings.Add((src, dest) =>
                    {
                        var val = destProp.GetValue(src) as string;
                        if (Enum.TryParse(srcProp.PropertyType, val, out var parsed))
                            srcProp.SetValue(dest, parsed);
                    });
                }
            }
        }

        public bool TryGetMapping((Type SourceType, Type DestinationType) key, out List<Action<object, object>> mappings)
        {
            return _mappings.TryGetValue(key, out mappings);
        }

    }
}
