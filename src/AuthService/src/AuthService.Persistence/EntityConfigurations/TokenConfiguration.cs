using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Persistence.EntityConfigurations
{
    public class TokenConfiguration : IEntityTypeConfiguration<Token>
    {
        public void Configure(EntityTypeBuilder<Token> builder)
        {
            builder.HasIndex(t => t.ValueHashed);

            builder.Property(t => t.Purpose)
                .HasConversion<EnumConverter<ETokenPurpose>>();
        }
    }
}
