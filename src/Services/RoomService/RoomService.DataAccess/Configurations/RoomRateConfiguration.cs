using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomService.Business.Entities;

namespace RoomService.DataAccess.Configurations;

public sealed class RoomRateConfiguration : IEntityTypeConfiguration<RoomRate>
{
    public void Configure(EntityTypeBuilder<RoomRate> builder)
    {
        builder.ToTable("RoomRates");

        builder.HasKey(rate => rate.Id);

        builder.Property(rate => rate.Amount)
            .HasPrecision(18, 2);

        builder.Property(rate => rate.ValidFrom)
            .IsRequired();

        builder.Property(rate => rate.ValidTo)
            .IsRequired();

        builder.Property(rate => rate.CreatedAtUtc)
            .IsRequired();

        builder.HasOne(rate => rate.RoomType)
            .WithMany(roomType => roomType.Rates)
            .HasForeignKey(rate => rate.RoomTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rate => new { rate.RoomTypeId, rate.ValidFrom, rate.ValidTo });
    }
}
