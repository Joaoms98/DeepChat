using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Galileo.Chat.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");

        b.HasKey(u => u.Id);

        b.Property(u => u.Username)
            .HasConversion(new UsernameConverter())
            .HasColumnName("Username")
            .HasMaxLength(Username.MaxLength)
            .IsRequired();

        b.HasIndex(u => u.Username).IsUnique();

        b.Property(u => u.Nickname)
            .HasConversion(new NicknameConverter())
            .HasColumnName("Nickname")
            .HasMaxLength(Nickname.MaxLength)
            .IsRequired();

        b.Property(u => u.PasswordHash)
            .HasMaxLength(256)
            .IsRequired();

        b.Property(u => u.CreatedAt).IsRequired();
        b.Property(u => u.LastLoginAt);
        b.Property(u => u.IsActive).IsRequired();
    }
}
