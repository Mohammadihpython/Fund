namespace SharedKernel;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    Problem = 2,
    NotFound = 3,
    Conflict = 4,
    BadRequest = 5,
    Unauthorized = 6,
    Forbidden = 7,
    InternalServerError = 8,
    NotImplemented = 9,
    BadGateway = 10,
    ServiceUnavailable = 11,
    Gateway = 12,
    Http = 13,
    Https = 14,
}
