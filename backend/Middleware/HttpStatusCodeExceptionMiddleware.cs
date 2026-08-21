using backend.common;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace backend.Middleware
{
    public class ErrorResult
    {
        public int Code { get; set; }
        public int Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }

    public class HttpStatusCodeExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HttpStatusCodeExceptionMiddleware> _logger;

        public HttpStatusCodeExceptionMiddleware(RequestDelegate next, ILogger<HttpStatusCodeExceptionMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (HttpStatusCodeException ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("The response has already started, the http status code middleware will not be executed.");
                    throw;
                }

                await HandleExceptionAsync(context, ex);
            }
            catch (Exception ex)
            {
                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("The response has already started, the http status code middleware will not be executed.");
                    throw;
                }

                await HandleExceptionAsync(context, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            context.Response.Clear();
            context.Response.ContentType = "application/json";

            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            var result = new ErrorResult
            {
                Code = StatusCodes.Status500InternalServerError,
                Status = StatusCodes.Status500InternalServerError,
                Message = "Oops! Something went wrong. Please try again later."
            };
            context.Response.StatusCode = result.Code;

            if (ex is HttpStatusCodeException httpEx)
            {
                context.Response.StatusCode = httpEx.StatusCode;
                result.Code = int.TryParse(httpEx.Code, out int code) && code > 0 ? code : httpEx.StatusCode;
                result.Message = httpEx.StatusCode == 500 ? "Oops! Something went wrong. Please try again later." : httpEx.Message;
                result.Status = httpEx.StatusCode;
                result.Data = httpEx.JsonData;
                _logger.LogError(httpEx.Message);
            }
            else if (ex is JsonReaderException && ex.Message.Contains("Could not convert string to DateTime"))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                result.Code = StatusCodes.Status400BadRequest;
                result.Message = ex.Message;
            }

            var content = string.Empty;
            if (ex.Message.Contains("isFluentError", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;

                try
                {
                    // Deserialize the error message to a FluentErrorResponse object
                    var fluentError = JsonConvert.DeserializeObject<FluentErrorResponse>(ex.Message);
                    if (fluentError != null)
                    {
                        var modifiedFluentErrorResponse = new
                        {
                            Code = StatusCodes.Status400BadRequest,
                            Status = StatusCodes.Status400BadRequest,
                            Message = fluentError.Message,
                            Errors = fluentError.Errors
                        };

                        content = JsonConvert.SerializeObject(modifiedFluentErrorResponse, settings);
                    }
                }
                catch
                {
                    content = JsonConvert.SerializeObject(result, settings);
                }
            }
            else if (ex is HttpStatusCodeException)
            {
                content = JsonConvert.SerializeObject(result, settings);
            }
            else
            {
                content = JsonConvert.SerializeObject(result, settings);
            }

            if (string.IsNullOrEmpty(content))
            {
                content = JsonConvert.SerializeObject(result, settings);
            }

            _logger.LogError(content);
            _logger.LogError(ex.StackTrace);
            if (ex.InnerException != null)
                _logger.LogError(ex.InnerException.ToString());

            return context.Response.WriteAsync(content);
        }
    }

    public static class HttpStatusCodeExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseHttpStatusCodeExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<HttpStatusCodeExceptionMiddleware>();
        }
    }
}
