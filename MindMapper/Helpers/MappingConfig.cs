using System.Linq.Expressions;
using System.Reflection;

namespace MindMapper
{
    public class MappingConfig<TSource, TDestination>
    {
        private readonly MappingProfile _profile;
        private readonly List<Action<TSource, TDestination>> _propertyMappings = new();
        private readonly HashSet<string> _ignoredProperties = new();
        private readonly HashSet<string> _explicitlyMappedProperties = new();
        private bool _autoMapCalled = false;

        public MappingConfig(MappingProfile profile)
        {
            _profile = profile;
        }

        public MappingConfig<TSource, TDestination> ForMember<TMember>(
      Expression<Func<TDestination, TMember>> destProperty,
      Func<TSource, TMember> sourceValue)
        {
            MemberExpression memberExpr = destProperty.Body switch
            {
                MemberExpression m => m,
                UnaryExpression u when u.Operand is MemberExpression m => m,
                _ => throw new ArgumentException("Must be a property expression like x => x.Property", nameof(destProperty))
            };

            var destPropName = memberExpr.Member.Name;
            _explicitlyMappedProperties.Add(destPropName);

            _propertyMappings.Add((src, dest) =>
            {
                var value = sourceValue(src);
                var prop = typeof(TDestination).GetProperty(destPropName);
                prop?.SetValue(dest, value);
            });

            return this;
        }


        public MappingConfig<TSource, TDestination> Ignore<TProperty>(Expression<Func<TDestination, TProperty>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                _ignoredProperties.Add(memberExpression.Member.Name);
            }
            else if (propertyExpression.Body is UnaryExpression unaryExpression &&
                     unaryExpression.Operand is MemberExpression unaryMemberExpression)
            {
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

        public MappingConfig<TDestination, TSource> ReverseMap()
        {
            var reverseConfig = new MappingConfig<TDestination, TSource>(_profile);

            foreach (var propName in _explicitlyMappedProperties)
            {
                var srcProp = typeof(TSource).GetProperty(propName);
                var destProp = typeof(TDestination).GetProperty(propName);

                if (srcProp == null || destProp == null || srcProp.PropertyType != destProp.PropertyType)
                    continue;

                var param = Expression.Parameter(typeof(TSource), "src");
                var body = Expression.Convert(Expression.Property(param, srcProp), typeof(object));
                var srcExpr = Expression.Lambda<Func<TSource, object>>(body, param);

                Func<TDestination, object> destValueFunc = dest => destProp.GetValue(dest);

                reverseConfig.ForMember(srcExpr, destValueFunc);
            }

            foreach (var ignored in _ignoredProperties)
            {
                var srcProp = typeof(TSource).GetProperty(ignored);
                if (srcProp == null)
                    continue;

                var param = Expression.Parameter(typeof(TSource), "src");
                var body = Expression.Property(param, srcProp);
                var delegateType = typeof(Func<,>).MakeGenericType(typeof(TSource), srcProp.PropertyType);
                var lambda = Expression.Lambda(delegateType, body, param);

                // Get the generic Ignore<TProperty> method
                var ignoreMethod = typeof(MappingConfig<TDestination, TSource>)
                    .GetMethod("Ignore")
                    ?.MakeGenericMethod(srcProp.PropertyType);

                ignoreMethod?.Invoke(reverseConfig, new object[] { lambda });
            }


            _profile.CreateMap<TDestination, TSource>(cfg =>
            {
                foreach (var act in reverseConfig._propertyMappings)
                {
                    cfg._propertyMappings.Add(act);
                }
            });

            return reverseConfig;
        }

        internal Action<TSource, TDestination> CompileMappingAction()
        {
            if (!_autoMapCalled)
            {
                ApplyAutoMap();
                _autoMapCalled = true;
            }

            return (src, dest) =>
            {
                foreach (var mapping in _propertyMappings)
                {
                    mapping(src, dest);
                }
            };
        }

        private void ApplyAutoMap()
        {
            var sourceType = typeof(TSource);
            var destinationType = typeof(TDestination);

            var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead);

            var destinationProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p =>
                    p.CanWrite &&
                    !_ignoredProperties.Contains(p.Name) &&
                    !_explicitlyMappedProperties.Contains(p.Name));

            foreach (var destProp in destinationProperties)
            {
                var srcProp = sourceProperties.FirstOrDefault(p =>
                    p.Name == destProp.Name &&
                    p.PropertyType == destProp.PropertyType);

                if (srcProp != null)
                {
                    AddPropertyMapping(srcProp, destProp);
                    continue;
                }

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

            var sourcePropExpr = Expression.Property(sourceParam, srcProp);
            var assignExpr = Expression.Assign(Expression.Property(destParam, destProp), sourcePropExpr);

            var lambda = Expression.Lambda<Action<TSource, TDestination>>(assignExpr, sourceParam, destParam);
            _propertyMappings.Add(lambda.Compile());
        }


        private void AddEnumToStringMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "src");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");

            var sourcePropExpr = Expression.Property(sourceParam, srcProp);
            var toStringCall = Expression.Call(sourcePropExpr, "ToString", null);

            var assignExpr = Expression.Assign(Expression.Property(destParam, destProp), toStringCall);

            var lambda = Expression.Lambda<Action<TSource, TDestination>>(assignExpr, sourceParam, destParam);
            _propertyMappings.Add(lambda.Compile());
        }

        private void AddStringToEnumMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "src");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");

            var sourcePropExpr = Expression.Property(sourceParam, srcProp);
            var tempVar = Expression.Variable(destProp.PropertyType, "temp");

            var tryParseCall = Expression.Call(
                typeof(Enum),
                "TryParse",
                new[] { destProp.PropertyType },
                Expression.Constant(destProp.PropertyType),
                sourcePropExpr,
                Expression.Constant(true),
                tempVar);

            var resultExpr = Expression.Condition(
                tryParseCall,
                tempVar,
                Expression.Default(destProp.PropertyType));

            var assignExpr = Expression.Assign(Expression.Property(destParam, destProp), resultExpr);
            var block = Expression.Block(new[] { tempVar }, assignExpr);

            var lambda = Expression.Lambda<Action<TSource, TDestination>>(block, sourceParam, destParam);
            _propertyMappings.Add(lambda.Compile());
        }
    }
}
