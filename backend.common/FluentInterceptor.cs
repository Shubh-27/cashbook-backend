using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using ValidationResult = FluentValidation.Results.ValidationResult;

namespace backend.common
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
                    Code = 400,
                    Status = 400,
                    Message = "Requested model is not valid"
                };

                var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                var instanceType = validationContext.InstanceToValidate?.GetType();

                foreach (var item in result.Errors)
                {
                    var prop = instanceType?.GetProperty(item.PropertyName);
                    var jsonName = prop?.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                        ?? prop?.GetCustomAttribute<Newtonsoft.Json.JsonPropertyAttribute>()?.PropertyName
                        ?? item.PropertyName;

                    // Add under JSON name (e.g. account_name, account_sid)
                    if (!dict.TryGetValue(jsonName, out var list))
                    {
                        list = new List<string>();
                        dict[jsonName] = list;
                    }
                    list.Add(item.ErrorMessage);

                    // If property name differs (e.g. AccountName vs account_name), also add under property name
                    if (!string.Equals(jsonName, item.PropertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!dict.TryGetValue(item.PropertyName, out var propList))
                        {
                            propList = new List<string>();
                            dict[item.PropertyName] = propList;
                        }
                        propList.Add(item.ErrorMessage);
                    }
                }
                errorResponse.Errors = dict;
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
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the error is represented in a fluent format.
        /// </summary>
        public bool IsFluentError { get; set; }

        /// <summary>
        /// Gets or sets a dictionary containing detailed error information keyed by property/field name.
        /// </summary>
        public Dictionary<string, List<string>>? Errors { get; set; }
    }
}

