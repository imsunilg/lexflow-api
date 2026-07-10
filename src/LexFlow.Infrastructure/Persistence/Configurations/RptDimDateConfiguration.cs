using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptDimDateConfiguration : IEntityTypeConfiguration<RptDimDate>
{
    public void Configure(EntityTypeBuilder<RptDimDate> builder)
    {
        builder.ToTable("rpt_dim_date", "rpt");
        builder.HasKey(d => d.DateKey);

        builder.Property(d => d.DateKey).HasColumnName("date_key");
        builder.Property(d => d.FullDate).HasColumnName("full_date").IsRequired();
        builder.Property(d => d.Year).HasColumnName("year").IsRequired();
        builder.Property(d => d.Quarter).HasColumnName("quarter").IsRequired();
        builder.Property(d => d.Month).HasColumnName("month").IsRequired();
        builder.Property(d => d.MonthName).HasColumnName("month_name").IsRequired();
        builder.Property(d => d.Day).HasColumnName("day").IsRequired();
        builder.Property(d => d.DayOfWeek).HasColumnName("day_of_week").IsRequired();
        builder.Property(d => d.DayName).HasColumnName("day_name").IsRequired();
        builder.Property(d => d.IsWeekend).HasColumnName("is_weekend").IsRequired();
        builder.Property(d => d.IsoWeek).HasColumnName("iso_week").IsRequired();
    }
}
