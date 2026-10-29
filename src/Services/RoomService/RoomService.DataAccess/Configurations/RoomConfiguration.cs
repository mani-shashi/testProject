using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomService.Business.Entities;

namespace RoomService.DataAccess.Configurations;

public sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");

        builder.HasKey(room => room.Id);

        builder.Property(room => room.Number)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(room => room.Floor)
            .IsRequired();

        builder.Property(room => room.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(room => room.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(room => room.RoomType)
            .WithMany(roomType => roomType.Rooms)
            .HasForeignKey(room => room.RoomTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(room => room.Blocks)
            .WithOne(block => block.Room)
            .HasForeignKey(block => block.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(room => room.Number)
            .IsUnique();

        builder.HasIndex(room => room.RoomTypeId);
        builder.HasIndex(room => room.Status);
    }
}
