using System.Text.Json.Serialization;
using SMBLibrary;
// The Smb* prefix avoids a collision with the AuthenticationMethod property below —
// a member name cannot shadow a type in return-type position.
using SmbAuthenticationMethod = SMBLibrary.Client.AuthenticationMethod;

namespace CES.API.FileStorage.Smb
{
    // Bound from FileStorage:Smb (i.e. FileStorage__Smb__<Name> as an environment
    // variable). Only Server/ShareName/Domain/Username/Password/BasePath need to be
    // supplied; everything else has a working default.
    //
    // Password is [JsonIgnore]d and excluded from ToString() so it cannot reach a
    // diagnostic response or a log line by accident.
    public class SmbOptions
    {
        // Hostname or FQDN — not a UNC path, so no leading "\\".
        public string Server { get; set; } = string.Empty;

        // Share name only, no server and no sub-path. A "$" suffix (hidden share) is fine.
        public string ShareName { get; set; } = string.Empty;

        // AD domain for the service account. Empty makes NTLM defer to the server's own
        // domain, which is a legitimate value to try (spec, Environment).
        public string Domain { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        [JsonIgnore]
        public string Password { get; set; } = string.Empty;

        // Path inside the share, above {locationId}. Empty means the share root is the
        // accepted root.
        public string BasePath { get; set; } = string.Empty;

        public string TransportType { get; set; } = SmbConstants.TransportDirectTcp;

        public string AuthenticationMethod { get; set; } = SmbConstants.AuthNtlmV2;

        public int BufferSize { get; set; } = SmbConstants.DefaultBufferSize;

        public int MaxConcurrentSessions { get; set; } = SmbConstants.DefaultMaxConcurrentSessions;

        public int ConnectTimeoutMs { get; set; } = SmbConstants.DefaultConnectTimeoutMs;

        public int MaxRetryAttempts { get; set; } = SmbConstants.DefaultMaxRetryAttempts;

        public int InitialRetryDelayMs { get; set; } = SmbConstants.DefaultInitialRetryDelayMs;

        // Read-back SHA256 verification after a write (Stage 3). Exposed as an escape
        // hatch for a share that turns out to be too slow, rather than hard-coding the
        // trade-off.
        public bool VerifyAfterWrite { get; set; } = true;

        // Stage 1 only: a small file under BasePath the health endpoint reads to prove
        // the read path end to end. Empty skips the probe.
        public string ProbeFile { get; set; } = string.Empty;

        // Gates /api/dev/smb/health independently of ASPNETCORE_ENVIRONMENT, so the
        // diagnostic can be turned on in a deployed environment without running it in
        // Development mode. In Development the endpoint is available regardless.
        public bool DiagnosticsEnabled { get; set; }

        public SMBTransportType ResolveTransportType() => TransportType switch
        {
            var t when Matches(t, SmbConstants.TransportDirectTcp)
                    || Matches(t, nameof(SMBTransportType.DirectTCPTransport)) => SMBTransportType.DirectTCPTransport,

            var t when Matches(t, SmbConstants.TransportNetBios)
                    || Matches(t, nameof(SMBTransportType.NetBiosOverTCP)) => SMBTransportType.NetBiosOverTCP,

            _ => throw new InvalidOperationException(
                $"Unknown FileStorage:Smb:TransportType '{TransportType}'. " +
                $"Supported: {SmbConstants.TransportDirectTcp}, {SmbConstants.TransportNetBios}."),
        };

        public SmbAuthenticationMethod ResolveAuthenticationMethod() => AuthenticationMethod switch
        {
            var a when Matches(a, SmbConstants.AuthNtlmV2) => SmbAuthenticationMethod.NTLMv2,
            var a when Matches(a, SmbConstants.AuthNtlmV1ExtendedSessionSecurity) => SmbAuthenticationMethod.NTLMv1ExtendedSessionSecurity,
            var a when Matches(a, SmbConstants.AuthNtlmV1) => SmbAuthenticationMethod.NTLMv1,

            _ => throw new InvalidOperationException(
                $"Unknown FileStorage:Smb:AuthenticationMethod '{AuthenticationMethod}'. " +
                $"Supported: {SmbConstants.AuthNtlmV2}, {SmbConstants.AuthNtlmV1ExtendedSessionSecurity}, {SmbConstants.AuthNtlmV1}."),
        };

        // Deliberately omits Password: this type ends up in log scopes and diagnostic
        // output, and a secret that is never formatted cannot be leaked by formatting.
        public override string ToString() =>
            $"SmbOptions {{ Server = {Server}, ShareName = {ShareName}, Domain = {Domain}, " +
            $"Username = {Username}, Password = ***, BasePath = {BasePath}, " +
            $"TransportType = {TransportType}, AuthenticationMethod = {AuthenticationMethod} }}";

        private static bool Matches(string value, string expected) =>
            string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
