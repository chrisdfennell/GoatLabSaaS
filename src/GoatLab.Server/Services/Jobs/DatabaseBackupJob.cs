using GoatLab.Server.Services.Backup;

namespace GoatLab.Server.Services.Jobs;

// Thin Hangfire-invocable wrapper over BackupService so job registration is
// clean and BackupService stays testable / reusable (ad-hoc admin "run now"
// button could call the service directly later).
public class DatabaseBackupJob
{
    private readonly IBackupService _backup;

    public DatabaseBackupJob(IBackupService backup) => _backup = backup;

    public Task RunAsync(CancellationToken cancellationToken)
        => _backup.RunAsync(cancellationToken);
}
