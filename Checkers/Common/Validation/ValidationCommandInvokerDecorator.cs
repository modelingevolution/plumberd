using FluentValidation;
using ModelingEvolution.Plumberd;

namespace Checkers.Common.Validation
{
    class ValidationCommandInvokerDecorator : ICommandInvoker
    {
        private readonly ICommandInvoker _next;
        private readonly ValidatorFactory _validatorFactory;

        public ValidationCommandInvokerDecorator(ICommandInvoker next, ValidatorFactory validatorFactoryFactory)
        {
            _next = next;
            _validatorFactory = validatorFactoryFactory;
        }
        public Task Execute(Guid id, ICommand c, Guid userId, Guid sessionId)
        {
            return Execute(id, c, new CommandInvocationContext(id, c, userId, sessionId));
        }
        public async Task Execute(Guid id, ICommand c, IContext context = null)
        {
            if (c != null)
            {
                var validator = _validatorFactory.GetValidatorFor(c.GetType());

                if (validator != null)
                {
                    var validationResult = validator.Validate(c);
                    if (validationResult.IsValid)
                    {
                        
                        await _next.Execute(id, c, context);
                    }
                    else throw new ValidationException(validationResult.Errors);
                }
                else
                {
                    
                    await _next.Execute(id, c, context);
                }
            }
            else throw new ValidationException("Command cannot be empty");
        }
    }
}