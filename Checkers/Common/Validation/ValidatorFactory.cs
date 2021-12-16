using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using FluentValidation.Results;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace Checkers.Common.Validation
{
    class ValidatorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private ConcurrentDictionary<Type, Func<IValidatorAdapter>> _cache;
        public ValidatorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _cache = new ConcurrentDictionary<Type, Func<IValidatorAdapter>>();
        }

        public IValidatorAdapter GetValidatorFor(Type t)
        {
            // will return null if validator cannot be constructed.
            return _cache.GetOrAdd(t, (type) =>
            {
                try
                {
                    var adapterType = typeof(IValidatorAdapter<>).MakeGenericType(type);
                    var tmp = (IValidatorAdapter)_serviceProvider.GetService(adapterType);
                    if (tmp == null)
                        return () => new DataAnnotationsValidationAdapter();
                    return () => (IValidatorAdapter)_serviceProvider.GetService(adapterType);
                }
                catch (Exception e)
                {
                    return () => new DataAnnotationsValidationAdapter();
                }
            })() ;
            
        }

        
    }
    internal class DataAnnotationsValidationAdapter : IValidatorAdapter
    {
        public ValidationResult Validate(object u)
        {
            var ctx = new ValidationContext(u);

            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            if (!Validator.TryValidateObject(u, ctx, results, true))
            {
                List<ValidationFailure> list = new List<ValidationFailure>();
                foreach (var i in results)
                foreach (var f in i.MemberNames)
                    list.Add(new ValidationFailure(f, i.ErrorMessage));
                return new ValidationResult(list);
            }
            return new ValidationResult();
        }
    }

}