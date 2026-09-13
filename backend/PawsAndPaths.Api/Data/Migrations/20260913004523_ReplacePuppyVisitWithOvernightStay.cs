using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsAndPaths.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePuppyVisitWithOvernightStay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "DurationMinutes", "IsOvernightStay", "Name", "Price" },
                values: new object[] { "Overnight companionship with morning care, a midday visit, and evening care. Times can be customized for each stay.", 660, true, "Overnight stay", 95m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Description", "DurationMinutes", "IsOvernightStay", "Name", "Price" },
                values: new object[] { "A gentle potty, play, feeding, and routine-building visit for young pups.", 30, false, "Puppy visit", 26m });
        }
    }
}
