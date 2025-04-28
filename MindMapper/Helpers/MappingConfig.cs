using System.Linq.Expressions;

namespace MindMapper
{
    public class MappingConfig<TSource, TDestination>
    {
        private readonly MappingProfile _profile;
        private readonly List<(Func<TSource, object> getter, Action<TDestination, object> setter)> _propertyMappings = new();
        internal HashSet<string> IgnoredProperties { get; } = new();

        public MappingConfig(MappingProfile profile)
        {
            _profile = profile;
        }

        public MappingConfig<TSource, TDestination> ReverseMap()
        {
            _profile.CreateReverseMap(this);
            return this;
        }

        public void ForMember<TMember>(Action<TDestination, TMember> setDest, Func<TSource, TMember> getSource)
        {
            _propertyMappings.Add(
                (src => getSource(src),
                 (dest, value) => setDest(dest, (TMember)value))
            );
        }

        public MappingConfig<TSource, TDestination> Ignore<TMember>(Expression<Func<TDestination, TMember>> destSelector)
        {
            if (destSelector.Body is MemberExpression memberExpr)
            {
                IgnoredProperties.Add(memberExpr.Member.Name);
            }
            else if (destSelector.Body is UnaryExpression unary && unary.Operand is MemberExpression unaryMember)
            {
                IgnoredProperties.Add(unaryMember.Member.Name);
            }
            else
            {
                throw new InvalidOperationException("Invalid expression passed to Ignore.");
            }

            return this;
        }

        internal (Func<object, object>, Action<TSource, TDestination>) CompileMappings()
        {
            // Create new instance function
            var newDestExpr = Expression.Lambda<Func<TDestination>>(
                Expression.New(typeof(TDestination))).Compile();

            // Compile the mapping function (object → object)
            var sourceParam = Expression.Parameter(typeof(object), "source");
            var typedSourceParam = Expression.Convert(sourceParam, typeof(TSource));

            var destVar = Expression.Variable(typeof(TDestination), "dest");
            var assignDest = Expression.Assign(destVar, Expression.Invoke(Expression.Constant(newDestExpr)));

            var expressions = new List<Expression> { assignDest };

            foreach (var (getter, setter) in _propertyMappings)
            {
                var value = Expression.Invoke(Expression.Constant(getter), typedSourceParam);
                var setExpr = Expression.Invoke(Expression.Constant(setter),
                    destVar,
                    Expression.Convert(value, typeof(object)));
                expressions.Add(setExpr);
            }

            expressions.Add(Expression.Convert(destVar, typeof(object)));

            var mappingFunc = Expression.Lambda<Func<object, object>>(
                Expression.Block(new[] { destVar }, expressions),
                sourceParam).Compile();

            // Compile the mapping action (TSource → TDestination)
            var sourceParam2 = Expression.Parameter(typeof(TSource), "source");
            var destParam = Expression.Parameter(typeof(TDestination), "dest");

            var actionExpressions = new List<Expression>();
            foreach (var (getter, setter) in _propertyMappings)
            {
                var value = Expression.Invoke(Expression.Constant(getter), sourceParam2);
                var setExpr = Expression.Invoke(Expression.Constant(setter),
                    destParam,
                    Expression.Convert(value, typeof(object)));
                actionExpressions.Add(setExpr);
            }

            var mappingAction = Expression.Lambda<Action<TSource, TDestination>>(
                Expression.Block(actionExpressions),
                sourceParam2, destParam).Compile();

            return (mappingFunc, mappingAction);
        }
    }
}
