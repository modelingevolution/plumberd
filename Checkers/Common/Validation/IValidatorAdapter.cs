using FluentValidation.Results;

namespace Checkers.Common.Validation
{
    interface IValidatorAdapter
    {
        ValidationResult Validate(object u);
    }
    interface IValidatorAdapter<T>  : IValidatorAdapter {}
}