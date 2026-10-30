using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StockCore.Api.Errors;

/// <summary>
/// Roda o validador do FluentValidation de cada argumento da action (corpo ou query)
/// antes do controller. Se houver erro, responde 400 com a lista de campos.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var failure in result.Errors)
            {
                var key = JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName);
                if (!errors.TryGetValue(key, out var messages))
                {
                    messages = [];
                    errors[key] = messages;
                }

                messages.Add(failure.ErrorMessage);
            }
        }

        if (errors.Count > 0)
        {
            var problem = ProblemFactory.Validation(
                context.HttpContext, errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
            context.Result = ProblemFactory.ToResult(problem);
            return;
        }

        await next();
    }
}
