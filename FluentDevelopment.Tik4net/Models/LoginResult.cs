

namespace FluentDevelopment.Tik4net.Models;

public class LoginResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private LoginResult(bool isSuccess, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static LoginResult Success() => new LoginResult(true);
    public static LoginResult Failure(string errorMessage, Exception? exception = null)
        => new LoginResult(false, errorMessage, exception);
}