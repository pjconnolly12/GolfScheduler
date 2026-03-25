using Microsoft.EntityFrameworkCore;
using MyApp.Models;

namespace MyApp.Data;

public static class DevelopmentRoundSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (await dbContext.Rounds.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.Rounds.AddRange(
            new Round
            {
                Date = DateTime.Today.AddDays(3),
                Course = "Pebble Beach",
                Notes = "Morning tee time",
                Holes = 18,
                PlayerLimit = 4
            },
            new Round
            {
                Date = DateTime.Today.AddDays(10),
                Course = "Augusta National",
                Notes = "Afternoon round",
                Holes = 18,
                PlayerLimit = 4
            });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
