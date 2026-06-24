using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using N2.Circulation.Api.Data;

#nullable disable

namespace N2.Circulation.Api.Data.Migrations
{
    [DbContext(typeof(CirculationDbContext))]
    partial class CirculationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0");

            modelBuilder.Entity("N2.Circulation.Api.Models.BorrowTransaction", b =>
            {
                b.Property<Guid>("Id");
                b.Property<string>("BookId").IsRequired().HasMaxLength(64);
                b.Property<DateTime>("BorrowedAt");
                b.Property<string>("CardNumber").HasMaxLength(32);
                b.Property<DateTime>("DueAt");
                b.Property<decimal>("FineAmount").HasPrecision(18, 2);
                b.Property<string>("Isbn").HasMaxLength(32);
                b.Property<DateTime?>("ReturnedAt");
                b.Property<string>("UserId").IsRequired().HasMaxLength(64);

                b.HasKey("Id");
                b.HasIndex("BookId");
                b.HasIndex("BorrowedAt");
                b.HasIndex("UserId", "CardNumber");

                b.ToTable("BorrowTransactions");
            });

            modelBuilder.Entity("N2.Circulation.Api.Models.FineCharge", b =>
            {
                b.Property<Guid>("Id");
                b.Property<Guid>("BorrowTransactionId");
                b.Property<decimal>("Amount").HasPrecision(18, 2);
                b.Property<string>("CardNumber").HasMaxLength(32);
                b.Property<DateTime>("CreatedAt");
                b.Property<DateTime?>("PaidAt");
                b.Property<DateTime?>("PaymentRequestedAt");
                b.Property<string>("PaymentStatus").IsRequired().HasMaxLength(32);
                b.Property<string>("Reason").IsRequired().HasMaxLength(256);
                b.Property<string>("UserId").IsRequired().HasMaxLength(64);

                b.HasKey("Id");
                b.HasIndex("UserId", "PaymentStatus", "PaidAt");

                b.ToTable("FineCharges");
            });

            modelBuilder.Entity("N2.Circulation.Api.Models.PublishedEventLog", b =>
            {
                b.Property<Guid>("Id");
                b.Property<string>("EventType").IsRequired().HasMaxLength(64);
                b.Property<string>("PayloadJson").IsRequired();
                b.Property<DateTime>("PublishedAt");
                b.Property<string>("SourceService").IsRequired().HasMaxLength(64);

                b.HasKey("Id");
                b.HasIndex("SourceService", "EventType", "PublishedAt");

                b.ToTable("PublishedEventLogs");
            });
        }
    }
}
