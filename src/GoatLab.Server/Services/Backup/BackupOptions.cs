namespace GoatLab.Server.Services.Backup;

// Offsite database backup target. S3-compatible — works with AWS S3,
// Backblaze B2 (via S3 endpoint), Wasabi, DigitalOcean Spaces, MinIO, etc.
public class BackupOptions
{
    public const string SectionName = "Backup:Offsite";

    /// <summary>Offsite uploads are opt-in. Leave false to skip the daily upload job.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>S3 endpoint URL. Leave blank for AWS S3 default. For B2: https://s3.us-west-001.backblazeb2.com</summary>
    public string? Endpoint { get; set; }

    public string Region { get; set; } = "us-east-1";

    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    /// <summary>Key prefix (folder) inside the bucket. E.g. "goatlab/db/".</summary>
    public string Prefix { get; set; } = "goatlab/db/";

    /// <summary>
    /// Delete backups in the bucket older than this many days. 0 = keep forever.
    /// Objects outside the prefix are never touched.
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}
