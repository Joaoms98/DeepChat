using Galileo.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Galileo.Chat.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ToTable("Sessions");

        b.HasKey(s => s.Id);

        b.Property(s => s.UserId).IsRequired();
        b.Property(s => s.JwtId).IsRequired();
        b.Property(s => s.IssuedAt).IsRequired();
        b.Property(s => s.ExpiresAt).IsRequired();
        b.Property(s => s.RevokedAt);
        b.Property(s => s.RemoteIp).HasMaxLength(45).IsRequired(); // IPv6 max len

        b.HasIndex(s => s.JwtId).IsUnique();
        b.HasIndex(s => s.UserId);
        b.HasIndex(s => s.ExpiresAt);

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
