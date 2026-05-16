using Galileo.Chat.Domain.Entities;
using Galileo.Chat.Domain.ValueObjects;
using Galileo.Chat.Infrastructure.Persistence.Conversions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Galileo.Chat.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("Rooms");

        b.HasKey(r => r.Id);

        b.Property(r => r.Name)
            .HasConversion(new RoomNameConverter())
            .HasColumnName("Name")
            .HasMaxLength(RoomName.MaxLength)
            .IsRequired();

        b.HasIndex(r => r.Name).IsUnique();

        b.Property(r => r.Salt)
            .HasColumnType("BLOB")
            .IsRequired();

        b.Property(r => r.CreatedAt).IsRequired();
    }
}
