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
            migrationBuilder.Sql("DELETE FROM [Rounds] WHERE [Id] IN (1, 2);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT [Rounds] ON;
                INSERT INTO [Rounds] ([Id], [Course], [Date], [Golfers], [Holes], [Notes], [Organizer], [PlayerLimit], [ReminderSentAtUtc])
                VALUES
                    (1, 'Pebble Beach', '2026-01-09T00:00:00', 1, 18, 'Morning tee time', '', 4, NULL),
                    (2, 'Augusta National', '2026-01-16T00:00:00', 1, 18, 'Afternoon round', '', 4, NULL);
                SET IDENTITY_INSERT [Rounds] OFF;
            ");
        }
    }
}
