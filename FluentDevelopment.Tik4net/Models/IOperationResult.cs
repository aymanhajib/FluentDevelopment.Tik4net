using System;

namespace FluentDevelopment.Tik4net.Models
{
    /// <summary>
    /// Represents the result of an operation, including success status, data, error information, and execution time.
    /// </summary>
    /// <typeparam name="T">The type of the data returned by the operation.</typeparam>
    public interface IOperationResult<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets the data returned by the operation, if any.
        /// </summary>
        T? Data { get; }

        /// <summary>
        /// Gets the error message if the operation failed; otherwise, <c>null</c>.
        /// </summary>
        string? ErrorMessage { get; }

        /// <summary>
        /// Gets the exception that occurred during the operation, if any.
        /// </summary>
        Exception? Exception { get; }

        /// <summary>
        /// Gets the time taken to execute the operation.
        /// </summary>
        TimeSpan ExecutionTime { get; }
    }

    /// <summary>
    /// Represents the result of an operation, including success status, error information, and execution time.
    /// </summary>
    public interface IOperationResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message if the operation failed; otherwise, <c>null</c>.
        /// </summary>
        string? ErrorMessage { get; }

        /// <summary>
        /// Gets the exception that occurred during the operation, if any.
        /// </summary>
        Exception? Exception { get; }

        /// <summary>
        /// Gets the time taken to execute the operation.
        /// </summary>
        TimeSpan ExecutionTime { get; }
    }
}
