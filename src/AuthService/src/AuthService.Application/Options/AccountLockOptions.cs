using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class AccountLockOptions : IOptions
    {
        public static string Key => "AccountLock";

        public required int NumberOfFailedAttempsBeforeLock { get; set; }
        public required int LockTimeInMinutes { get; set; }
        public required int MaxLockTimeMultiplier { get; set; }
    }
}
