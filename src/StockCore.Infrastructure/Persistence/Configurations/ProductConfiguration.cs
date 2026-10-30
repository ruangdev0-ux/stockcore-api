using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockCore.Domain.Entities;

namespace StockCore.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        // As regras de saldo também ficam no banco: nenhuma escrita deixa o estoque negativo.
        builder.ToTable("products", table =>
        {
            table.HasCheckConstraint("ck_products_current_stock_non_negative", "current_stock >= 0");
            table.HasCheckConstraint("ck_products_minimum_stock_non_negative", "minimum_stock >= 0");
            table.HasCheckConstraint("ck_products_unit_price_non_negative", "unit_price >= 0");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.UnitPrice).HasPrecision(12, 2);
        builder.HasIndex(p => p.CategoryId);

        // xmin é uma coluna de sistema do PostgreSQL que muda a cada UPDATE:
        // serve de token de concorrência sem criar coluna nova.
        builder.Property(p => p.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Ignore(p => p.IsLowStock);

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
