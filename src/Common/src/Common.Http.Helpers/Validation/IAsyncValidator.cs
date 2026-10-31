using Common.Http.Response.Structures;

namespace Common.Http.Validation
{
    /// <summary>
    /// Provides an async method to validate a value.
    /// </summary>
    /// <typeparam name="T">Value type to validate</typeparam>
    public interface IAsyncValidator<T>
    {
        /// <summary>
        /// Validates a given value.
        /// </summary>
        /// <param name="value">Value to validate</param>
        /// <param name="cancellationToken">Token to cancel the validation early</param>
        /// <returns>Returns a list of errors if there any. Otherwise it returns an empty list.</returns>
        Task<List<ErrorItem>> ValidateAsync(T value, CancellationToken cancellationToken = default);
    }
}
