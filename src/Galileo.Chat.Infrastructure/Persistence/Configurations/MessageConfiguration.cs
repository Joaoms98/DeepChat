using Galileo.Chat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Galileo.Chat.Infrastructure.Persistence.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> b)
    {
        b.ToTable("Messages");

        b.HasKey(m => m.Id);

        b.Property(m => m.SenderId).IsRequired();
        b.Property(m => m.RoomId);
        b.Property(m => m.RecipientId);
        b.Property(m => m.Kind).IsRequired().HasConversion<int>();
        b.Property(m => m.CreatedAt).IsRequired();

        // EncryptedPayload is an owned value object stored inline as 3 BLOBs.
        // EF Core binds the private constructor (byte[] iv, byte[] ciphertext, byte[] tag)
        // — parameter names match property names case-insensitively, so EF wires it
        // automatically. Reads go through the public byte[] getters which defensively
        // clone; that 3-allocation cost per Message is acceptable here (≤64KB payloads,
        // ≤200 concurrent users). Optimize via Field access mode if it ever shows up
        // in profiling.
        b.OwnsOne(m => m.Payload, payload =>
        {
            payload.Property(p => p.Iv)
                .HasColumnName("Iv")
                .HasColumnType("BLOB")
                .IsRequired();

            payload.Property(p => p.Ciphertext)
                .HasColumnName("Ciphertext")
                .HasColumnType("BLOB")
                .IsRequired();

            payload.Property(p => p.Tag)
                .HasColumnName("Tag")
                .HasColumnType("BLOB")
                .IsRequired();
        });

        b.Navigation(m => m.Payload).IsRequired();

        // Indexes
        b.HasIndex(m => new { m.RoomId, m.CreatedAt })
            .HasDatabaseName("IX_Messages_Room_Time");

        b.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("IX_Messages_CreatedAt"); // for purge scans

        b.HasIndex(m => new { m.SenderId, m.RecipientId, m.CreatedAt })
            .HasDatabaseName("IX_Messages_Direct");
    }
}
