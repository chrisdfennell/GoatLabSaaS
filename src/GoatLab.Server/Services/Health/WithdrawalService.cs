using GoatLab.Server.Data;
using GoatLab.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GoatLab.Server.Services.Health;

public enum WithdrawalKind { Milk, Meat }

public record ActiveWithdrawal(DateTime EndsAt, int RecordId, string? MedicationName, DateTime Administered);

public class WithdrawalService
{
    private readonly GoatLabDbContext _db;
    public WithdrawalService(GoatLabDbContext db) => _db = db;

    public async Task<ActiveWithdrawal?> GetActiveAsync(int goatId, WithdrawalKind kind)
    {
        var now = DateTime.UtcNow;
        var q = _db.MedicalRecords
            .Where(r => r.GoatId == goatId)
            .Include(r => r.Medication)
            .AsQueryable();

        q = kind == WithdrawalKind.Milk
            ? q.Where(r => r.MilkWithdrawalEndsAt > now)
            : q.Where(r => r.MeatWithdrawalEndsAt > now);

        var row = await q
            .OrderByDescending(r => kind == WithdrawalKind.Milk ? r.MilkWithdrawalEndsAt : r.MeatWithdrawalEndsAt)
            .FirstOrDefaultAsync();
        if (row is null) return null;

        var endsAt = (kind == WithdrawalKind.Milk ? row.MilkWithdrawalEndsAt : row.MeatWithdrawalEndsAt)!.Value;
        return new ActiveWithdrawal(endsAt, row.Id, row.Medication?.Name, row.Date);
    }
}
