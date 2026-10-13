namespace Shared.AspNetCore.Helpers.Options
{
    /// <summary>
    /// Marks options classes for automatic registration in the dependency injection.
    /// </summary>
    public interface IOptions
    {
        /// <summary>
        /// Top-level section name representing this options class.
        /// </summary>
        static abstract string Key { get; }
    }
}
