using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LanBot.Data;

public sealed class LanBotDbContextFactory : IDesignTimeDbContextFactory<LanBotDbContext>
{
    public LanBotDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
                   ?? "Host=localhost;Port=5432;Database=lanbot;Username=lanbot;Password=lanbot";

        var builder = new DbContextOptionsBuilder<LanBotDbContext>()
            .UseNpgsql(conn);

        return new LanBotDbContext(builder.Options);
    }
}

