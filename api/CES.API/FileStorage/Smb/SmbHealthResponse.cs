using System.Text.Json.Serialization;

namespace CES.API.FileStorage.Smb
{
    // The shape of GET /api/dev/smb/health. Deliberately progressive: each step runs
    // only if its prerequisite succeeded, and every step reports its own outcome, so one
    // call says exactly how far we got. The open unknowns — AD domain, share name, base
    // path — each fail at a different step (spec, Stage 1).
    public sealed class SmbHealthResponse
    {
        public SmbHealthSteps Steps { get; set; } = new();

        // What the server agreed to during negotiation. Populated once connect succeeds.
        // There is no dialect or "encrypted" field: SMBLibrary 1.5.3 keeps both private,
        // and a field that cannot be honestly filled is worse than an absent one.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SmbNegotiatedLimits? Negotiated { get; set; }

        public long ElapsedMs { get; set; }
    }

    public sealed class SmbHealthSteps
    {
        public SmbHealthStep Connect { get; set; } = new();
        public SmbLoginStep Login { get; set; } = new();
        public SmbListSharesStep ListShares { get; set; } = new();
        public SmbTreeConnectStep TreeConnect { get; set; } = new();
        public SmbListPathStep ListBasePath { get; set; } = new();
        public SmbProbeReadStep ProbeRead { get; set; } = new();
    }

    public class SmbHealthStep
    {
        public bool Ok { get; set; }

        // The raw NTStatus name, never flattened into a friendly string. Null when the
        // step failed below the SMB layer (DNS, TCP) and there was no status to read.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Status { get; set; }

        public long ElapsedMs { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Error { get; set; }

        // Why the step did not run at all — a missing setting, or a prerequisite step
        // that failed. Distinct from Error, which means it ran and failed.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Skipped { get; set; }
    }

    public sealed class SmbLoginStep : SmbHealthStep
    {
        // Echoed back so a run can be attributed to the domain it actually used —
        // the point of re-running with IDIR / PROVJUD / empty.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Domain { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Method { get; set; }
    }

    public sealed class SmbListSharesStep : SmbHealthStep
    {
        // Answers a share name we do not have yet. Goes through IPC$/SRVSVC, so a
        // hardened server may deny this while normal share access works — informative,
        // not fatal, and the run continues to treeConnect regardless.
        public IReadOnlyList<string> Shares { get; set; } = [];
    }

    public sealed class SmbTreeConnectStep : SmbHealthStep
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Share { get; set; }
    }

    public sealed class SmbListPathStep : SmbHealthStep
    {
        // Doubles as base-path discovery: with BasePath empty this lists the share root,
        // which is how we work out where the accepted root should sit.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? BasePath { get; set; }

        public IReadOnlyList<string> Entries { get; set; } = [];

        // True when the folder holds more than MaxDiagnosticDirectoryEntries names, so
        // a short list is not mistaken for a complete one.
        public bool Truncated { get; set; }
    }

    public sealed class SmbProbeReadStep : SmbHealthStep
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Path { get; set; }

        public long Bytes { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Sha256 { get; set; }
    }
}
