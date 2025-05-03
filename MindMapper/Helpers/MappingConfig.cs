using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    public class MappingConfig<TSource, TDestination>
    {
        private readonly MappingProfile _profile;
        private readonly List<Action<TSource, TDestination>> _propertyMappings = new();
        private readonly HashSet<string> _ignoredProperties = new();

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

            // Get all writable properties from destination
            var destinationProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && !_ignoredProperties.Contains(p.Name));

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

            // Register the reverse mapping
            var reverseKey = (typeof(TDestination), typeof(TSource));
            var reverseAction = reverseConfig.CompileMappingAction();
            _profile._mappings[reverseKey] = (src, dest) => reverseAction((TDestination)src, (TSource)dest);

            return this;
        }

        public void ForMember<TMember>(Action<TDestination, TMember> setDest, Func<TSource, TMember> getSource)
        {
            _propertyMappings.Add((src, dest) => setDest(dest, getSource(src)));
        }

        public MappingConfig<TSource, TDestination> Ignore<TMember>(Expression<Func<TDestination, TMember>> destSelector)
        {
            if (destSelector.Body is MemberExpression memberExpr)
            {
                _ignoredProperties.Add(memberExpr.Member.Name);
            }
            else if (destSelector.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
            {
                _ignoredProperties.Add(unaryMember.Member.Name);
            }
            else
            {
                throw new InvalidOperationException("Invalid expression passed to Ignore.");
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
