using System.Linq.Expressions;
using System.Reflection;

namespace MindMapper
{
    public class MappingConfig<TSource, TDestination>
    {
        private readonly MappingProfile _profile;
        private readonly List<Action<TSource, TDestination>> _propertyMappings = new();
        private readonly HashSet<string> _ignoredProperties = new();
        private bool _autoMapCalled = false;
        public MappingConfig(MappingProfile profile)
        {
            _profile = profile;
            // Automatically apply AutoMap when config is created
            AutoMap();
        }

        public void AutoMap()
        {
            var sourceType = typeof(TSource);
            var destinationType = typeof(TDestination);

            // Get all readable properties from source
            var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead);


            // Get all writable properties from destination (excluding ignored ones)
            var destinationProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && !_ignoredProperties.Contains(p.Name));

            foreach (var destProp in destinationProperties)
            {
                if (_ignoredProperties.Contains(destProp.Name))
                    continue;

                var srcProp = sourceProperties.FirstOrDefault(p =>
                    p.Name == destProp.Name &&
                    p.PropertyType == destProp.PropertyType);

                if (srcProp != null)
                {
                    AddPropertyMapping(srcProp, destProp);
                    continue;
                }

                // Handle enum-string conversions
                if (srcProp?.PropertyType.IsEnum == true && destProp.PropertyType == typeof(string))
                {
                    AddEnumToStringMapping(srcProp, destProp);
                }
                else if (srcProp?.PropertyType == typeof(string) && destProp.PropertyType.IsEnum)
                {
                    AddStringToEnumMapping(srcProp, destProp);
                }
            }

        }

        private void AddPropertyMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "src");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");

            // src.Property
            var sourcePropExpr = Expression.Property(sourceParam, srcProp);

            // dest.Property = src.Property
            var assignExpr = Expression.Assign(
                Expression.Property(destParam, destProp),
                sourcePropExpr);

            // Compile to: (src, dest) => dest.Property = src.Property
            var lambda = Expression.Lambda<Action<TSource, TDestination>>(
                assignExpr, sourceParam, destParam);

            _propertyMappings.Add(lambda.Compile());
        }

        private void AddEnumToStringMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "src");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");

            // src.Property?.ToString()
            var sourcePropExpr = Expression.Property(sourceParam, srcProp);
            var toStringCall = Expression.Call(sourcePropExpr, "ToString", null);

            // dest.Property = src.Property?.ToString()
            var assignExpr = Expression.Assign(
                Expression.Property(destParam, destProp),
                toStringCall);

            var lambda = Expression.Lambda<Action<TSource, TDestination>>(
                assignExpr, sourceParam, destParam);

            _propertyMappings.Add(lambda.Compile());
        }

        private void AddStringToEnumMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "src");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");

            // Enum.TryParse(typeof(DestEnum), src.Property, out var temp) ? temp : default
            var sourcePropExpr = Expression.Property(sourceParam, srcProp);
            var tempVar = Expression.Variable(destProp.PropertyType, "temp");

            var tryParseCall = Expression.Call(
                typeof(Enum),
                "TryParse",
                new[] { destProp.PropertyType },
                Expression.Constant(destProp.PropertyType),
                sourcePropExpr,
                Expression.Constant(true), // ignoreCase
                tempVar);

            var resultExpr = Expression.Condition(
                tryParseCall,
                tempVar,
                Expression.Default(destProp.PropertyType));

            // dest.Property = Enum.TryParse(...) ? temp : default
            var assignExpr = Expression.Assign(
                Expression.Property(destParam, destProp),
                resultExpr);

            var block = Expression.Block(
                new[] { tempVar },
                assignExpr);

            var lambda = Expression.Lambda<Action<TSource, TDestination>>(
                block, sourceParam, destParam);

            _propertyMappings.Add(lambda.Compile());
        }


        public MappingConfig<TSource, TDestination> ReverseMap()
        {
            var reverseConfig = new MappingConfig<TDestination, TSource>(_profile);

            // Convert our mappings to reverse mappings
            foreach (var mapping in _propertyMappings)
            {
                reverseConfig._propertyMappings.Add((dest, src) => mapping(src, dest));
            }


            // Compile the reverse mapping action
            var reverseAction = reverseConfig.CompileMappingAction();

            // Register in all relevant dictionaries
            var reverseKey = (typeof(TDestination), typeof(TSource));
            var untypedReverseAction = (Action<object, object>)((src, dest) => reverseAction((TDestination)src, (TSource)dest));

            _profile._mappings[reverseKey] = untypedReverseAction;
            _profile._typedMappings[reverseKey] = untypedReverseAction;
            _profile._mappingActions[reverseKey] = reverseAction;

            return this;
        }

        public void ForMember<TMember>(Action<TDestination, TMember> setDest, Func<TSource, TMember> getSource)
        {
            _propertyMappings.Add((src, dest) => setDest(dest, getSource(src)));
        }

        public MappingConfig<TSource, TDestination> Ignore<TProperty>(Expression<Func<TDestination, TProperty>> propertyExpression)
        {
            if (propertyExpression == null)
                throw new ArgumentNullException(nameof(propertyExpression));

            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                _ignoredProperties.Add(memberExpression.Member.Name);
            }
            else if (propertyExpression.Body is UnaryExpression unaryExpression &&
                    unaryExpression.Operand is MemberExpression unaryMemberExpression)
            {
                // Handle conversions like x => x.SomeProperty (which becomes Convert(x.SomeProperty))
                _ignoredProperties.Add(unaryMemberExpression.Member.Name);
            }
            else
            {
                throw new ArgumentException(
                    "The expression must be a simple property access expression like 'x => x.PropertyName'",
                    nameof(propertyExpression));
            }

            return this;
        }

        internal Action<TSource, TDestination> CompileMappingAction()
        {
            return (src, dest) =>
            {
                foreach (var mapping in _propertyMappings)
                {
                    mapping(src, dest);
                }
            };
        }
    }
}
