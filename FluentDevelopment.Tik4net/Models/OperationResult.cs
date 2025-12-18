
using System;

namespace FluentDevelopment.Tik4net.Models
{
    public class OperationResult<T> : IOperationResult<T>
    {
        public bool IsSuccess { get; }
        public T? Data { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }
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

        public static OperationResult<T> Success(T data, TimeSpan executionTime)
            => new(data, true, null, null, executionTime);

        public static OperationResult<T> Failure(string errorMessage,
            Exception? exception = null, TimeSpan executionTime = default)
            => new(default, false, errorMessage, exception, executionTime);
    }

    public class OperationResult : IOperationResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public Exception? Exception { get; }
        public TimeSpan ExecutionTime { get; }

        private OperationResult(bool isSuccess, string? errorMessage,
            Exception? exception, TimeSpan executionTime)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
            ExecutionTime = executionTime;
        }

        public static OperationResult Success(TimeSpan executionTime)
            => new(true, null, null, executionTime);

        public static OperationResult Failure(string errorMessage,
            Exception? exception = null, TimeSpan executionTime = default)
            => new(false, errorMessage, exception, executionTime);
    }
}
