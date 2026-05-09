

using System;

namespace FluentDevelopment.Tik4net.Models;

/// <summary>
/// Represents the result of a login attempt.
/// </summary>
public class LoginResult
{
    /// <summary>
    /// Gets a value indicating whether the login was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the error message if the login failed; otherwise, <c>null</c>.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the exception that occurred during login, if any; otherwise, <c>null</c>.
    /// </summary>
    public Exception? Exception { get; }

    private LoginResult(bool isSuccess, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    /// <summary>
    /// Creates a <see cref="LoginResult"/> representing a successful login.
    /// </summary>
    /// <returns>A successful <see cref="LoginResult"/>.</returns>
    public static LoginResult Success() => new(true);

    /// <summary>
    /// Creates a <see cref="LoginResult"/> representing a failed login.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="exception">The exception that occurred, if any.</param>
    /// <returns>A failed <see cref="LoginResult"/>.</returns>
    public static LoginResult Failure(string errorMessage, Exception? exception = null)
        => new(false, errorMessage, exception);
}