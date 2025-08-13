using System.Collections.Concurrent;
using System.Reflection;

namespace MindMapper
{

    public class Mapper : IMapper
    {
        private readonly IMappingProfile _profile;
        private readonly ConcurrentDictionary<TypePair, Delegate> _mappingCache = new();

        public Mapper(IMappingProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public TDestination Map<TDestination>(object source) where TDestination : new()
        {
            if (source == null) return default;

            var sourceType = source.GetType();
            var typePair = new TypePair(sourceType, typeof(TDestination));

            // Check for same type mapping
            if (sourceType == typeof(TDestination))
            {
                return (TDestination)source;
            }

            var mappingAction = _mappingCache.GetOrAdd(typePair, pair =>
            {
                // First try to get from profile
                if (_profile.TryGetMapping(pair.SourceType, pair.DestinationType, out var profileAction))
                {
                    return profileAction;
                }

                // If no profile mapping, try auto-mapping for compatible types
                if (TryCreateAutoMapper(pair.SourceType, pair.DestinationType, out var autoMapper))
                {
                    return autoMapper;
                }

                throw new InvalidOperationException($"No mapping found from {pair.SourceType} to {pair.DestinationType}");
            });

            var destination = new TDestination();
            mappingAction.DynamicInvoke(source, destination);
            return destination;
        }

        public List<TDestination> Map<TDestination>(IEnumerable<object> sources) where TDestination : new()
        {
            if (sources == null) return new List<TDestination>();

            // Optimize for ICollection to pre-allocate list size
            var list = sources is ICollection<object> collection
                ? new List<TDestination>(collection.Count)
                : new List<TDestination>();

            foreach (var item in sources)
            {
                list.Add(Map<TDestination>(item));
            }
            return list;
        }

        public List<TDestination> Map<TSource, TDestination>(IEnumerable<TSource> sources)
            where TDestination : new()
        {
            if (sources == null)
                return new List<TDestination>();

            var mappingFunc = GetCachedMappingFunction<TSource, TDestination>();

            var list = sources switch
            {
                ICollection<TSource> collection => new List<TDestination>(collection.Count),
                IReadOnlyCollection<TSource> readOnlyCollection => new List<TDestination>(readOnlyCollection.Count),
                _ => new List<TDestination>()
            };

            foreach (var item in sources)
            {
                var destination = new TDestination();
                mappingFunc(item, destination);
                list.Add(destination);
            }

            return list;
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            var mappingAction = GetCachedMappingFunction<TSource, TDestination>();
            mappingAction(source, destination);
            return destination;
        }
        private Action<TSource, TDestination> GetCachedMappingFunction<TSource, TDestination>()
        {
            var typePair = new TypePair(typeof(TSource), typeof(TDestination));

            var mappingAction = _mappingCache.GetOrAdd(typePair, pair =>
            {
                if (!_profile.TryGetMapping<TSource, TDestination>(out var action))
                {
                    throw new InvalidOperationException($"No mapping found from {pair.SourceType} to {pair.DestinationType}");
                }
                return action;
            });

            return (Action<TSource, TDestination>)mappingAction;
        }
        private bool TryCreateAutoMapper(Type sourceType, Type destinationType, out Delegate mapper)
        {
            // Simple case - same type
            if (sourceType == destinationType)
            {
                mapper = (Action<object, object>)((src, dest) => { });
                return true;
            }

            // TODO: Add more sophisticated auto-mapping logic here if needed
            // For example, property-by-property copying for similar types

            mapper = null;
            return false;
        }

        private readonly struct TypePair : IEquatable<TypePair>
        {
            public Type SourceType { get; }
            public Type DestinationType { get; }

            public TypePair(Type sourceType, Type destinationType)
            {
                SourceType = sourceType;
                DestinationType = destinationType;
            }

            public bool Equals(TypePair other) =>
                SourceType == other.SourceType && DestinationType == other.DestinationType;

            public override bool Equals(object obj) => obj is TypePair other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(SourceType, DestinationType);
        }
    }

}
