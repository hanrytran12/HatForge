using System.Net;
using System.Text.Json;
using HatForge.Application.Common;

namespace HatForge.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        string response;

        switch (exception)
        {
            case ValidationException validationEx when validationEx.Errors.Count > 0:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = JsonSerializer.Serialize(
                    ApiResponse<object>.FailValidation(exception.Message, validationEx.Errors),
                    JsonOptions);
                break;

            case NotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response = JsonSerializer.Serialize(
                    ApiResponse<object>.Fail(exception.Message), JsonOptions);
                break;

            case ValidationException:
            case BusinessRuleException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response = JsonSerializer.Serialize(
                    ApiResponse<object>.Fail(exception.Message), JsonOptions);
                break;

            case UnauthorizedException:
            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response = JsonSerializer.Serialize(
                    ApiResponse<object>.Fail(exception.Message), JsonOptions);
                break;

            case ForbiddenException:
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                response = JsonSerializer.Serialize(
                    ApiResponse<object>.Fail(exception.Message), JsonOptions);
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response = JsonSerializer.Serialize(
                    ApiResponse<object>.Fail("An unexpected error occurred"), JsonOptions);
                break;
        }

        await context.Response.WriteAsync(response);
    }
}
