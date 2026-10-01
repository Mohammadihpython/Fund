namespace SharedKernel;

public static class ResultExtensions
{
    public static ResponseModel<T> ToResponse<T>(
        this Result<T> result,
        string? traceId = null)
    {
        if (result.IsSuccess)
        {
            return new ResponseModel<T>
            {
                StatusCode = StatusCodes.Status200OK,
                Message = null,
                Data = result.Value,
                Errors = null,
                TraceId = traceId,
                Timestamp = DateTime.UtcNow
            };
        }

        var error = result.Error!;

        return new ResponseModel<T>
        {
            StatusCode = error.Type.ToStatusCode(),
            Message = GetMessage(error.Type),
            Data = default,
            Errors = error.Description,
            TraceId = traceId,
            Timestamp = DateTime.UtcNow
        };
    }

    private static int ToStatusCode(this ErrorType type)
        => type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.BadRequest => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetMessage(ErrorType type)
        => type switch
        {
            ErrorType.NotFound => "موجودیت یافت نشد",
            ErrorType.Validation => "فرمت فیلد اشتباه است",
            ErrorType.Conflict => "موجودیت از قبل وجود دارد",
            ErrorType.Unauthorized => "دسترسی غیرمجاز",
            ErrorType.Forbidden => "دسترسی ممنوع است",
            ErrorType.BadRequest => "درخواست نامعتبر است",
            _ => "خطایی در سرور رخ داده است"
        };
}