using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace N2.Circulation.Api.Data.Migrations
{
    public partial class AddFinePaymentWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "FineCharges",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentRequestedAt",
                table: "FineCharges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.DropIndex(
                name: "IX_FineCharges_UserId_PaidAt",
                table: "FineCharges");

            migrationBuilder.CreateIndex(
                name: "IX_FineCharges_UserId_PaymentStatus_PaidAt",
                table: "FineCharges",
                columns: new[] { "UserId", "PaymentStatus", "PaidAt" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FineCharges_UserId_PaymentStatus_PaidAt",
                table: "FineCharges");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "FineCharges");

            migrationBuilder.DropColumn(
                name: "PaymentRequestedAt",
                table: "FineCharges");

            migrationBuilder.CreateIndex(
                name: "IX_FineCharges_UserId_PaidAt",
                table: "FineCharges",
                columns: new[] { "UserId", "PaidAt" });
        }
    }
}
