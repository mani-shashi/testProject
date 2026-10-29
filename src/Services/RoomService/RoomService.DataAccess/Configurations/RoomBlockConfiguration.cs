using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RoomService.Business.Entities;

namespace RoomService.DataAccess.Configurations;

public sealed class RoomBlockConfiguration : IEntityTypeConfiguration<RoomBlock>
{
    public void Configure(EntityTypeBuilder<RoomBlock> builder)
    {
        builder.ToTable("RoomBlocks");

        builder.HasKey(block => block.Id);

        builder.Property(block => block.FromDate)
            .IsRequired();

        builder.Property(block => block.ToDate)
            .IsRequired();

        builder.Property(block => block.Reason)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(block => block.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(block => new { block.RoomId, block.FromDate, block.ToDate });
    }
}
