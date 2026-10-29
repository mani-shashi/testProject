using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomService.Business.Entities;

namespace RoomService.DataAccess.Configurations;

public sealed class RoomTypeConfiguration : IEntityTypeConfiguration<RoomType>
{
    public void Configure(EntityTypeBuilder<RoomType> builder)
    {
        builder.ToTable("RoomTypes");

        builder.HasKey(roomType => roomType.Id);

        builder.Property(roomType => roomType.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(roomType => roomType.Description)
            .HasMaxLength(500);

        builder.Property(roomType => roomType.BasePrice)
            .HasPrecision(18, 2);

        builder.Property(roomType => roomType.MaxOccupancy)
            .IsRequired();

        builder.Property(roomType => roomType.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(roomType => roomType.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(roomType => roomType.Name)
            .IsUnique();
    }
}
