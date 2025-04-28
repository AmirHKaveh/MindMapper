namespace MindMapper
{
    public class MappingProfile
    {
        private readonly Dictionary<(Type source, Type destination), Func<object, object>> _mappingFuncs = new();
        private readonly Dictionary<(Type source, Type destination), Delegate> _mappingActions = new();

        public MappingConfig<TSource, TDestination> CreateMap<TSource, TDestination>(Action<MappingConfig<TSource, TDestination>> config = null)
        {
            var mapConfig = new MappingConfig<TSource, TDestination>(this);

            config?.Invoke(mapConfig);

            var (mappingFunc, mappingAction) = mapConfig.CompileMappings();

            var key = (typeof(TSource), typeof(TDestination));
            _mappingFuncs[key] = mappingFunc;
            _mappingActions[key] = mappingAction;

            return mapConfig;
        }

        public void CreateReverseMap<TSource, TDestination>(MappingConfig<TSource, TDestination> originalConfig)
        {
            var reverseConfig = new MappingConfig<TDestination, TSource>(this);

            foreach (var ignoredProp in originalConfig.IgnoredProperties)
            {
                reverseConfig.IgnoredProperties.Add(ignoredProp);
            }

            var (mappingFunc, mappingAction) = reverseConfig.CompileMappings();

            var key = (typeof(TDestination), typeof(TSource));
            _mappingFuncs[key] = mappingFunc;
            _mappingActions[key] = mappingAction;
        }

        public bool TryGetMapping(Type sourceType, Type destType, out Func<object, object> mappingFunc)
        {
            var key = (sourceType, destType);
            return _mappingFuncs.TryGetValue(key, out mappingFunc);
        }

        public bool TryGetMappingAction<TSource, TDestination>(out Action<TSource, TDestination> mappingAction)
        {
            var key = (typeof(TSource), typeof(TDestination));
            if (_mappingActions.TryGetValue(key, out var action))
            {
                mappingAction = (Action<TSource, TDestination>)action;
                return true;
            }
            mappingAction = null;
            return false;
        }

        internal bool TryGetMappingAction(Type sourceType, Type destType, out Action<object, object> mappingAction)
        {
            var key = (sourceType, destType);
            if (_mappingActions.TryGetValue(key, out var action))
            {
                mappingAction = (src, dest) => ((Delegate)action).DynamicInvoke(src, dest);
                return true;
            }
            mappingAction = null;
            return false;
        }
    }
}
