using System.Linq.Expressions;
using System.Reflection;

namespace MindMapper
{
    public class MappingConfig<TSource, TDestination>
    {
        private readonly MappingProfile _profile;

        // store property-specific mappings (can overwrite same property cleanly)
        private readonly Dictionary<string, Action<TSource, TDestination>> _propertyMappings = new();

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
            var memberExpr = destProperty.Body switch
            {
                MemberExpression m => m,
                UnaryExpression u when u.Operand is MemberExpression m => m,
                _ => throw new ArgumentException("Must be a property expression like x => x.Property", nameof(destProperty))
            };

            var destPropName = memberExpr.Member.Name;

            if (_ignoredProperties.Contains(destPropName))
                return this;

            _explicitlyMappedProperties.Add(destPropName);

            _propertyMappings[destPropName] = (src, dest) =>
            {
                var value = sourceValue(src);
                var prop = typeof(TDestination).GetProperty(destPropName);
                prop?.SetValue(dest, value);
            };

            return this;
        }

        public MappingConfig<TSource, TDestination> Ignore<TProperty>(
            Expression<Func<TDestination, TProperty>> propertyExpression)
        {
            string propertyName;

            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                propertyName = memberExpression.Member.Name;
            }
            else if (propertyExpression.Body is UnaryExpression unaryExpression &&
                     unaryExpression.Operand is MemberExpression unaryMemberExpression)
            {
                propertyName = unaryMemberExpression.Member.Name;
            }
            else
            {
                throw new ArgumentException(
                    "The expression must be a simple property access expression like 'x => x.PropertyName'",
                    nameof(propertyExpression));
            }

            _ignoredProperties.Add(propertyName);
            _explicitlyMappedProperties.Remove(propertyName);
            _propertyMappings.Remove(propertyName); // remove any mapping if it already exists

            return this;
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
                foreach (var mapping in _propertyMappings.Values)
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

        public MappingConfig<TDestination, TSource> ReverseMap()
        {
            var reverseConfig = new MappingConfig<TDestination, TSource>(_profile);

            // Carry over ignored properties
            foreach (var ignored in _ignoredProperties)
            {
                reverseConfig._ignoredProperties.Add(ignored);
                reverseConfig._propertyMappings.Remove(ignored);
            }

            // Carry over explicit mappings (only if same-type properties exist)
            foreach (var propName in _explicitlyMappedProperties)
            {
                var sourceProp = typeof(TSource).GetProperty(propName);
                var destProp = typeof(TDestination).GetProperty(propName);

                if (sourceProp == null || destProp == null)
                    continue;

                if (sourceProp.PropertyType == destProp.PropertyType)
                {
                    reverseConfig._propertyMappings[propName] = (src, dest) =>
                    {
                        var value = sourceProp.GetValue(dest); // notice: reverse direction
                        destProp.SetValue(src, value);
                    };

                    reverseConfig._explicitlyMappedProperties.Add(propName);
                }
            }

            // Compile and register reverse mapping
            var mappingAction = reverseConfig.CompileMappingAction();
            var untypedAction = (Action<object, object>)((src, dest) =>
                mappingAction((TDestination)src, (TSource)dest));

            var key = (typeof(TDestination), typeof(TSource));
            _profile._mappings[key] = untypedAction;
            _profile._mappingActions[key] = mappingAction;

            return reverseConfig;
        }


        private void AddPropertyMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            _propertyMappings[destProp.Name] = (src, dest) =>
            {
                var value = srcProp.GetValue(src);
                destProp.SetValue(dest, value);
            };
        }

        private void AddEnumToStringMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            _propertyMappings[destProp.Name] = (src, dest) =>
            {
                var value = srcProp.GetValue(src)?.ToString();
                destProp.SetValue(dest, value);
            };
        }

        private void AddStringToEnumMapping(PropertyInfo srcProp, PropertyInfo destProp)
        {
            _propertyMappings[destProp.Name] = (src, dest) =>
            {
                var strValue = srcProp.GetValue(src) as string;
                if (Enum.TryParse(destProp.PropertyType, strValue, true, out var enumValue))
                {
                    destProp.SetValue(dest, enumValue);
                }
                else
                {
                    destProp.SetValue(dest, Activator.CreateInstance(destProp.PropertyType));
                }
            };
        }
    }

}
