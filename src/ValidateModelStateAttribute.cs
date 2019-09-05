// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HelixPoolProvider
{
    public class ValidateModelStateAttribute : TypeFilterAttribute
    {
        public ValidateModelStateAttribute() : base(typeof(ValidateModelStateImpl))
        {
            Order = int.MaxValue;
        }

        private class ValidateModelStateImpl : IActionFilter
        {
            private readonly ILogger<ValidateModelStateAttribute> _logger;

            public ValidateModelStateImpl(ILogger<ValidateModelStateAttribute> logger)
            {
                _logger = logger;
            }

            public void OnActionExecuting(ActionExecutingContext context)
            {
                if (context.ModelState.IsValid)
                {
                    return;
                }

                LogValidationFailures(context);

                context.Result = new BadRequestObjectResult(context.ModelState);
            }

            private void LogValidationFailures(ActionContext context)
            {
                StringBuilder errorString = new StringBuilder();
                foreach ((string prop, ModelStateEntry entry) in context.ModelState)
                {
                    if (entry.Errors == null || entry.Errors.Count <= 0)
                    {
                        continue;
                    }

                    if (errorString.Length != 0)
                    {
                        errorString.Append(" | ");
                    }

                    errorString.Append(prop);
                    errorString.Append(" : ");

                    errorString.AppendJoin(", ", entry.Errors.Select(e => e.ErrorMessage));
                }

                _logger.LogWarning("Invalid view state detected: {message}", errorString.ToString());
            }

            public void OnActionExecuted(ActionExecutedContext context)
            {
            }
        }
    }
}
