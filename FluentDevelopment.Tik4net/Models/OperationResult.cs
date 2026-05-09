using System;

namespace FluentDevelopment.Tik4net.Models
{
    /// <summary>
    /// Represents the result of an operation, including success status, data, error information, and execution time.
    /// </summary>
    /// <typeparam name="T">The type of the data returned by the operation.</typeparam>
    public class OperationResult<T> : IOperationResult<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the data returned by the operation, if successful.
        /// </summary>
        public T? Data { get; }

        /// <summary>
        /// Gets the error message if the operation failed; otherwise, <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the exception associated with a failed operation, if any.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the time taken to execute the operation.
        /// </summary>
        public TimeSpan ExecutionTime { get; }

        private OperationResult(T? data, bool isSuccess,
            string? errorMessage, Exception? exception, TimeSpan executionTime)
        {
            Data = data;
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            ExecutionTime = executionTime;
        }

        /// <summary>
        /// Creates a successful <see cref="OperationResult{T}"/> with the specified data and execution time.
        /// </summary>
        /// <param name="data">The data returned by the operation.</param>
        /// <param name="executionTime">The time taken to execute the operation.</param>
        /// <returns>A successful <see cref="OperationResult{T}"/> instance.</returns>
        public static OperationResult<T> Success(T data, TimeSpan executionTime)
            => new(data, true, null, null, executionTime);

        /// <summary>
        /// Creates a failed <see cref="OperationResult{T}"/> with the specified error message, exception, and execution time.
        /// </summary>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <param name="exception">The exception associated with the failure, if any.</param>
        /// <param name="executionTime">The time taken to execute the operation.</param>
        /// <returns>A failed <see cref="OperationResult{T}"/> instance.</returns>
        public static OperationResult<T> Failure(string errorMessage,
            Exception? exception = null, TimeSpan executionTime = default)
            => new(default, false, errorMessage, exception, executionTime);
    }

    /// <summary>
    /// Represents the result of an operation, including success status, error information, and execution time.
    /// </summary>
    public class OperationResult : IOperationResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message if the operation failed; otherwise, <c>null</c>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the exception associated with a failed operation, if any.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the time taken to execute the operation.
        /// </summary>
        public TimeSpan ExecutionTime { get; }

        private OperationResult(bool isSuccess, string? errorMessage,
            Exception? exception, TimeSpan executionTime)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            ExecutionTime = executionTime;
        }

        /// <summary>
        /// Creates a successful <see cref="OperationResult"/> with the specified execution time.
        /// </summary>
        /// <param name="executionTime">The time taken to execute the operation.</param>
        /// <returns>A successful <see cref="OperationResult"/> instance.</returns>
        public static OperationResult Success(TimeSpan executionTime)
            => new(true, null, null, executionTime);

        /// <summary>
        /// Creates a failed <see cref="OperationResult"/> with the specified error message, exception, and execution time.
        /// </summary>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <param name="exception">The exception associated with the failure, if any.</param>
        /// <param name="executionTime">The time taken to execute the operation.</param>
        /// <returns>A failed <see cref="OperationResult"/> instance.</returns>
        public static OperationResult Failure(string errorMessage,
            Exception? exception = null, TimeSpan executionTime = default)
            => new(false, errorMessage, exception, executionTime);
    }
}
