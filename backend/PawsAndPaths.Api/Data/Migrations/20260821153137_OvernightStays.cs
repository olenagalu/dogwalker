using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PawsAndPaths.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OvernightStays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOvernightStay",
                table: "Services",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Bookings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOvernightStay",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MiddayEndTime",
                table: "Bookings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "MiddayStartTime",
                table: "Bookings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OvernightEndTime",
                table: "Bookings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "OvernightStartTime",
                table: "Bookings",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsOvernightStay",
                value: false);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 2,
                column: "IsOvernightStay",
                value: false);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 3,
                column: "IsOvernightStay",
                value: false);

            migrationBuilder.UpdateData(
                table: "Services",
                keyColumn: "Id",
                keyValue: 4,
                column: "IsOvernightStay",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOvernightStay",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsOvernightStay",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MiddayEndTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "MiddayStartTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OvernightEndTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "OvernightStartTime",
                table: "Bookings");
        }
    }
}
