using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockCore.Domain.Entities;

namespace StockCore.Infrastructure.Persistence.Configurations;

public sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements", table =>
            table.HasCheckConstraint("ck_stock_movements_quantity_positive", "quantity > 0"));

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.UnitCost).HasPrecision(12, 2);
        builder.Property(m => m.Note).HasMaxLength(500);
        builder.Property(m => m.CancellationReason).HasMaxLength(500);

        builder.HasIndex(m => new { m.ProductId, m.CreatedAt });
        builder.HasIndex(m => m.Type);
        // Um movimento só pode ter um estorno.
        builder.HasIndex(m => m.ReversalOfId).IsUnique();

        builder.HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.CancelledById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StockMovement>()
            .WithMany()
            .HasForeignKey(m => m.ReversalOfId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
