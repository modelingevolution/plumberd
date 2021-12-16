using FluentValidation;
using FluentValidation.Results;
using ModelingEvolution.Plumberd;
using ModelingEvolution.Plumberd.StateTransitioning;

namespace Checkers.Common.Validation
{
    class AggregateInvokerValidationDecorator<T> : IAggregateInvoker<T> where T : class, IRootAggregate, new()
    {
        private readonly IAggregateInvoker<T> _next;
        private readonly ValidatorFactory _validatorFactory;
        public AggregateInvokerValidationDecorator(IAggregateInvoker<T> next, ValidatorFactory validatorFactory)
        {
            _next = next;
            _validatorFactory = validatorFactory;
        }

        public async Task<IExecuteResult<T>> Execute<TCommand>(Guid id, TCommand cmd) where TCommand : ICommand
        {
            var validator = _validatorFactory.GetValidatorFor(typeof(TCommand));
            if (validator != null)
            {
                var result = validator.Validate(cmd);
                if (result.IsValid)
                    return await _next.Execute(id, cmd);
                else return new ExecuteResult<T>(null, new ValidationException(result.Errors));
            }
            else return await _next.Execute(id, cmd);
        }

        public async Task<IExecuteResult<T>> Execute<TCommand>(T aggregate, TCommand cmd) where TCommand : ICommand
        {
            var validator = _validatorFactory.GetValidatorFor(typeof(TCommand));
            if (validator != null)
            {
                var result = validator.Validate(cmd);
                if (result.IsValid)
                    return await _next.Execute(aggregate, cmd);
                else return new ExecuteResult<T>(aggregate, new ValidationException(result.Errors));
            }
            else return await _next.Execute(aggregate, cmd);
        }

    }
    class ValidatorAdapter<T> : IValidatorAdapter<T>
    {
        private readonly FluentValidation.IValidator<T> _validator;

        public ValidatorAdapter(FluentValidation.IValidator<T> validator)
        {
            _validator = validator;
        }

        ValidationResult IValidatorAdapter.Validate(object u)
        {
            return _validator.Validate((T)u);
        }
    }
}