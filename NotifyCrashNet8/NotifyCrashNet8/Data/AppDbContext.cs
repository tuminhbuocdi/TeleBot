using Microsoft.EntityFrameworkCore;

namespace NotifyCrashNet8.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CrashEvent> CrashEvents => Set<CrashEvent>();
    public DbSet<CrashRecord> CrashRecords => Set<CrashRecord>();
}

public sealed class CrashEvent
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal Crash { get; set; }
}
