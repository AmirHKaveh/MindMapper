using System.Collections.Concurrent;

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

            var mappingAction = _mappingCache.GetOrAdd(typePair, pair =>
            {
                if (!_profile.TryGetMapping(pair.SourceType, pair.DestinationType, out Action<object, object> action))
                {
                    throw new InvalidOperationException($"No mappings found from {pair.SourceType} to {pair.DestinationType}");
                }
                return action;
            });

            var destination = new TDestination();
            ((Action<object, object>)mappingAction)(source, destination);
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

        private Action<TSource, TDestination> GetCachedMappingFunction<TSource, TDestination>()
        {
            if (!_profile.TryGetMapping<TSource, TDestination>(out var action))
            {
                throw new InvalidOperationException($"No mapping found from {typeof(TSource)} to {typeof(TDestination)}");
            }
            return action;
        }


        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            var typePair = new TypePair(typeof(TSource), typeof(TDestination));

            var mappingAction = _mappingCache.GetOrAdd(typePair, pair =>
            {
                if (!_profile.TryGetMapping<TSource, TDestination>(out Action<TSource, TDestination> action))
                {
                    throw new InvalidOperationException($"No mapping found from {pair.SourceType} to {pair.DestinationType}");
                }
                return action;
            });

            ((Action<TSource, TDestination>)mappingAction)(source, destination);
            return destination;
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
