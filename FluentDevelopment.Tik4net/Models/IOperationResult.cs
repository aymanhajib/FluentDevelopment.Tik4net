
using System;


namespace FluentDevelopment.Tik4net.Models
{
    public interface IOperationResult<T>
    {
        bool IsSuccess { get; }
        T? Data { get; }
        string? ErrorMessage { get; }
        Exception? Exception { get; }
        TimeSpan ExecutionTime { get; }
    }

    public interface IOperationResult
    {
        bool IsSuccess { get; }
        string? ErrorMessage { get; }
        Exception? Exception { get; }
        TimeSpan ExecutionTime { get; }
    }
}
