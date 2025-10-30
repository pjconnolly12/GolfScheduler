using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GolfScheduler.Migrations
{
    /// <inheritdoc />
    public partial class SeedTestData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Players",
                columns: new[] { "Id", "Email", "Name" },
                values: new object[,]
                {
                    { 1, "patrick@example.com", "Patrick Connolly" },
                    { 2, "jordan@example.com", "Jordan Smith" }
                });

            migrationBuilder.InsertData(
                table: "Rounds",
                columns: new[] { "Id", "Course", "Date", "Notes" },
                values: new object[,]
                {
                    { 1, "Pebble Beach", new DateTime(2025, 10, 29, 0, 0, 0, 0, DateTimeKind.Local), "Morning tee time" },
                    { 2, "Augusta National", new DateTime(2025, 11, 5, 0, 0, 0, 0, DateTimeKind.Local), "Afternoon round" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Players",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Players",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Rounds",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Rounds",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
