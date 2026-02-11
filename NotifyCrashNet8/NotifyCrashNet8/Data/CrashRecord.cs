using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotifyCrashNet8.Data;

public sealed class CrashRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long GameId { get; set; }

    public decimal Rate { get; set; }
}

public sealed class CrashRecordInput
{
    public required long GameId { get; set; }
    public required decimal Rate { get; set; }
}
