using System.Linq.Expressions;

namespace Mapator
{
    public class CustomMappingConfig<TSource, TDestination>
    {
        private readonly CustomMappingProfile _profile;
        internal List<Action<object, object>> PropertyMappings { get; } = new();
        internal HashSet<string> IgnoredProperties { get; } = new();

        public CustomMappingConfig(CustomMappingProfile profile)
        {
            _profile = profile;
        }

        public CustomMappingConfig<TSource, TDestination> ReverseMap()
        {
            _profile.CreateReverseMap(this);
            return this;
        }


        public void ForMember<TMember>(Action<TDestination, TMember> setDest, Func<TSource, TMember> getSource)
        {
            PropertyMappings.Add((src, dest) =>
            {
                var value = getSource((TSource)src);
                setDest((TDestination)dest, value);
            });
        }

        public CustomMappingConfig<TSource, TDestination> Ignore<TMember>(Expression<Func<TDestination, TMember>> destSelector)
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

    }
}
