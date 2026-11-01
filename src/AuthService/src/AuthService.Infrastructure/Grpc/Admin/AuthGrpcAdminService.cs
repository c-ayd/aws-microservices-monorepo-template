using System.Net;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Domain.Enums;
using Shared.Grpc.Auth.Admin;

namespace AuthService.Infrastructure.Grpc.Admin
{
    public partial class AuthGrpcAdminService : AuthGrpcAdmin.AuthGrpcAdminBase
    {
        private readonly IAuthDbContext _authDbContext;

        public AuthGrpcAdminService(IAuthDbContext authDbContext)
        {
            _authDbContext = authDbContext;
        }

        private class AccountDetailsDto
        {
            public required DateTimeOffset CreatedDate { get; set; }
            public required string Email { get; set; }
            public required bool IsEmailVerified { get; set; }
            public required bool IsBanned { get; set; }
            public required bool IsLocked { get; set; }
            public required int FailedLoginAttempts { get; set; }
            public DateTimeOffset? UnlockDate { get; set; }
            public DateTimeOffset? UpdatedDate { get; set; }
            public required bool IsDeleted { get; set; }
            public DateTimeOffset? DeletedDate { get; set; }

            public List<RoleDetailsDto> Roles { get; set; } = new List<RoleDetailsDto>();
            public List<SessionDetailsDto> Sessions { get; set; } = new List<SessionDetailsDto>();
            public List<TokenDetailsDto> Tokens { get; set; } = new List<TokenDetailsDto>();

            public AccountDto GetAccountDto()
            {
                var accountDto = new AccountDto();
                accountDto.CreatedDateInUnixTimeSeconds = CreatedDate.ToUnixTimeSeconds();
                accountDto.Email = Email;
                accountDto.IsEmailVerified = IsEmailVerified;
                accountDto.IsBanned = IsBanned;
                accountDto.IsLocked = IsLocked;
                accountDto.FailedLoginAttempts = FailedLoginAttempts;
                if (UnlockDate.HasValue)
                {
                    accountDto.UnlockDateInUnixTimeSeconds = UnlockDate.Value.ToUnixTimeSeconds();
                }
                if (UpdatedDate.HasValue)
                {
                    accountDto.UpdatedDateInUnixTimeSeconds = UpdatedDate.Value.ToUnixTimeSeconds();
                }
                accountDto.IsDeleted = accountDto.IsDeleted;
                if (DeletedDate.HasValue)
                {
                    accountDto.DeletedDateInUnixTimeSeconds = DeletedDate.Value.ToUnixTimeSeconds();
                }

                return accountDto;
            }
        }

        private class RoleDetailsDto
        {
            public required DateTimeOffset CreatedDate { get; set; }
            public required string Name { get; set; }

            public RoleDto GetRoleDto()
            {
                var roleDto = new RoleDto();
                roleDto.CreatedDateInUnixTimeSeconds = CreatedDate.ToUnixTimeSeconds();
                roleDto.Name = Name;

                return roleDto;
            }
        }

        private class SessionDetailsDto
        {
            public required DateTimeOffset CreatedDate { get; set; }
            public required DateTimeOffset ExpirationDate { get; set; }
            public IPAddress? IpAddress { get; set; }
            public string? DeviceInfo { get; set; }
            public DateTimeOffset? UpdatedDate { get; set; }

            public SessionDto GetSessionDto()
            {
                var sessionDto = new SessionDto();
                sessionDto.CreatedDateInUnixTimeSeconds = CreatedDate.ToUnixTimeSeconds();
                sessionDto.ExpirationDateInUnixTimeSeconds = ExpirationDate.ToUnixTimeSeconds();
                if (IpAddress != null)
                {
                    sessionDto.IpAddress = IpAddress.ToString();
                }
                if (DeviceInfo != null)
                {
                    sessionDto.DeviceInfo = DeviceInfo;
                }
                if (UpdatedDate.HasValue)
                {
                    sessionDto.UpdatedDateInUnixTimeSeconds = UpdatedDate.Value.ToUnixTimeSeconds();
                }

                return sessionDto;
            }
        }

        private class TokenDetailsDto
        {
            public required DateTimeOffset CreatedDate { get; set; }
            public ETokenPurpose Purpose { get; set; }
            public required DateTimeOffset ExpirationDate { get; set; }

            public TokenDto GetTokenDto()
            {
                var tokenDto = new TokenDto();
                tokenDto.CreatedDateInUnixTimeSeconds = CreatedDate.ToUnixTimeSeconds();
                tokenDto.Purpose = Purpose.ToString();
                tokenDto.ExpirationDateInUnixTimeSeconds = ExpirationDate.ToUnixTimeSeconds();

                return tokenDto;
            }
        }
    }
}
