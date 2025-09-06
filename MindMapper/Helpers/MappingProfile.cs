using System.Collections.Concurrent;

namespace MindMapper
{
    public class MappingProfile : IMappingProfile
    {
        public readonly Dictionary<(Type source, Type destination), Action<object, object>> _mappings = new();
        public readonly ConcurrentDictionary<(Type, Type), Delegate> _mappingActions = new();

        public MappingConfig<TSource, TDestination> CreateMap<TSource, TDestination>()
        {
            var mapConfig = new MappingConfig<TSource, TDestination>(this);

            // defer registration until CompileMappingAction
            var mappingAction = mapConfig.CompileMappingAction();
            var untypedAction = (Action<object, object>)((src, dest) => mappingAction((TSource)src, (TDestination)dest));

            var key = (typeof(TSource), typeof(TDestination));
            _mappings[key] = untypedAction;
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
            if (_mappingActions.TryGetValue(key, out var del))
            {
                mappingAction = (Action<TSource, TDestination>)del;
                return true;
            }

            mappingAction = null;
            return false;
        }
    }

}
