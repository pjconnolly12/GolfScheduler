using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyApp.Data;

#nullable disable

namespace GolfScheduler.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260325000000_RemoveRoundSeedDataFromSchema")]
    public class RemoveRoundSeedDataFromSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Rounds",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Rounds",
                keyColumn: "Id",
                keyValue: 2);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Rounds",
                columns: new[] { "Id", "Course", "Date", "Golfers", "Holes", "Notes", "Organizer", "PlayerLimit", "ReminderSentAtUtc" },
                values: new object[,]
                {
                    { 1, "Pebble Beach", new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Local), 1, 18, "Morning tee time", "", 4, null },
                    { 2, "Augusta National", new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Local), 1, 18, "Afternoon round", "", 4, null }
                });
        }
    }
}
