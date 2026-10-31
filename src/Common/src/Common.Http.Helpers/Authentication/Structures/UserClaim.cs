namespace Common.Http.Authentication.Structures
{
    /// <summary>
    /// Holds information about a user claim.
    /// </summary>
    /// <param name="ClaimType">Type of the claim in access tokens</param>
    /// <param name="HeaderKey">Name of the claim in headers</param>
    /// <param name="IsMultiple">Whether it appears more than once</param>
    public record UserClaim(
        string ClaimType,
        string HeaderKey,
        bool IsMultiple
    );
}
