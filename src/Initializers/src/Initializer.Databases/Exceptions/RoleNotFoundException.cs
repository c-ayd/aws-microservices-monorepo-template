namespace Initializer.Databases.Exceptions
{
    public class RoleNotFoundException : Exception
    {
        public RoleNotFoundException(string roleName)
            : base($"The role {roleName} does not exist in the DB or in the seed data options.")
        {
        }
    }
}
