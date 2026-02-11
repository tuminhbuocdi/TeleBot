using Microsoft.EntityFrameworkCore;
using NotifyCrashNet8.Data;

namespace NotifyCrashNet8.Services;

public sealed class CrashRecordSyncService
{
    private readonly AppDbContext _db;

    public CrashRecordSyncService(AppDbContext db)
    {
        _db = db;
    }

    public async Task SyncAsync(IEnumerable<CrashRecordInput> inputs, CancellationToken cancellationToken)
    {
        var inputList = inputs.ToList();
        var inputGameIds = inputList.Select(x => x.GameId).ToHashSet();

        var existing = await _db.CrashRecords
            .AsTracking()
            .ToListAsync(cancellationToken);

        var existingByGameId = existing.ToDictionary(x => x.GameId);

        foreach (var input in inputList)
        {
            if (existingByGameId.TryGetValue(input.GameId, out var entity))
            {
                if (entity.Rate != input.Rate)
                {
                    entity.Rate = input.Rate;
                }
            }
            else
            {
                _db.CrashRecords.Add(new CrashRecord
                {
                    GameId = input.GameId,
                    Rate = input.Rate,
                });
            }
        }

        var toDelete = existing.Where(x => !inputGameIds.Contains(x.GameId)).ToList();
        if (toDelete.Count > 0)
        {
            _db.CrashRecords.RemoveRange(toDelete);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
