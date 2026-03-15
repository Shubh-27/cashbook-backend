using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace backend.Helper
{
    public class FluentInterceptor : IValidatorInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the FluentInterceptor class.
        /// </summary>
        public FluentInterceptor()
        {

        }

        /// <summary>
        /// Invoked after ASP.NET validation.
        /// </summary>
        /// <param name="actionContext">The context of the action.</param>
        /// <param name="validationContext">The context of the validation.</param>
        /// <param name="result">The result of the validation.</param>
        /// <returns>The validation result.</returns>
        /// <exception cref="Exception">Thrown when validation fails.</exception>
        public ValidationResult AfterAspNetValidation(ActionContext actionContext, IValidationContext validationContext, ValidationResult result)
        {
            if (!result.IsValid)
            {
                var errorResponse = new FluentErrorResponse
                {
                    Code = 500,
                    Status = 400,
                    Message = "Requested model is not valid"
                };

                JObject jObject = new JObject();
                foreach (var item in result.Errors)
                {
                    if (!jObject.ContainsKey(item.PropertyName))
                        jObject.Add(item.PropertyName, item.ErrorMessage);
                }
                errorResponse.Errors = jObject;
                errorResponse.IsFluentError = true;
                throw new Exception(JsonConvert.SerializeObject(errorResponse));
            }
            return result;
        }

        /// <summary>
        /// Invoked before ASP.NET validation.
        /// </summary>
        /// <param name="actionContext">The context of the action.</param>
        /// <param name="commonContext">The context of the validation.</param>
        /// <returns>The modified validation context.</returns>
        public IValidationContext BeforeAspNetValidation(ActionContext actionContext, IValidationContext commonContext)
        {
            return commonContext;
        }
    }
    public class FluentErrorResponse
    {
        /// <summary>
        /// Gets or sets the numeric code associated with the current instance.
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Gets or sets the status code representing the current state of the operation.
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Gets or sets the message providing additional information about the current instance.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the error is represented in a fluent format.
        /// </summary>
        public bool IsFluentError { get; set; }

        /// <summary>
        /// Gets or sets a JObject containing detailed error information, where each key represents a property name and the corresponding value is the error message associated with that property.
        /// </summary>
        public JObject Errors { get; set; }
    }
}
