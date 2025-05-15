using System.Collections.Concurrent;

namespace MindMapper
{
    public class MappingProfile:IMappingProfile
    {
        public readonly Dictionary<(Type source, Type destination), Action<object, object>> _mappings = new();
        public readonly Dictionary<(Type source, Type destination), Action<object, object>> _typedMappings = new();
        public readonly ConcurrentDictionary<(Type, Type), Delegate> _mappingActions = new();

        public MappingConfig<TSource, TDestination> CreateMap<TSource, TDestination>(
     Action<MappingConfig<TSource, TDestination>> config = null)
        {
            var mapConfig = new MappingConfig<TSource, TDestination>(this);

            // 1. First apply all configurations (including Ignore calls)
            config?.Invoke(mapConfig);

            // 2. Then apply AutoMap (which will respect ignored properties)
            mapConfig.ApplyAutoMap();

            // 3. Compile the final mapping
            var mappingAction = mapConfig.CompileMappingAction();
            var untypedAction = (Action<object, object>)((src, dest) => mappingAction((TSource)src, (TDestination)dest));

            var key = (typeof(TSource), typeof(TDestination));
            _mappings[key] = untypedAction;
            _typedMappings[key] = untypedAction;
            _mappingActions[key] = mappingAction;

            return mapConfig;
        }

        public bool TryGetMapping(Type sourceType, Type destType, out Action<object, object> mappingAction)
        {
            var key = (sourceType, destType);
            return _mappings.TryGetValue(key, out mappingAction);
        }
        public bool TryGetMappingAction(Type sourceType, Type destType, out Delegate mappingAction)
        {
            return _mappingActions.TryGetValue((sourceType, destType), out mappingAction);
        }
        public bool TryGetMapping<TSource, TDestination>(out Action<TSource, TDestination> mappingAction)
        {
            var key = (typeof(TSource), typeof(TDestination));
            if (_typedMappings.TryGetValue(key, out var untypedAction))
            {
                mappingAction = (src, dest) => untypedAction(src, dest);
                return true;
            }

            mappingAction = null;
            return false;
        }
    }
}
