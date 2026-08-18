using CES.API.FileStorage.Smb;
using CES.Business.Constants;
using CES.Business.FileStorage;
using CES.Business.Interfaces;

namespace CES.API.FileStorage
{
    // Wires up the file store from configuration. The pending and accepted halves are
    // resolved separately, then composed by FileStorageCoordinator into the single
    // IFileStorage the services consume.
    public static class FileStorageRegistration
    {
        private const string SectionName = "FileStorage";

        // Removed in favour of PendingProvider/AcceptedProvider. Config binding ignores
        // unknown keys silently, so a deployment still setting the old key would look
        // configured while doing nothing — we fail at boot instead.
        private const string LegacyProviderKey = "Provider";

        // FileStorage:Smb — bound separately from StorageOptions so the SMB types can
        // take IOptions<SmbOptions> without dragging the whole storage config along.
        private const string SmbSectionName = "Smb";

        public static IServiceCollection AddFileStorage(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);

            if (section[LegacyProviderKey] != null)
                throw new InvalidOperationException(
                    $"{SectionName}:{LegacyProviderKey} is no longer used. The pending and accepted stores are " +
                    $"configured independently — set {SectionName}:PendingProvider and {SectionName}:AcceptedProvider instead.");

            services.Configure<StorageOptions>(section);

            RegisterSmbInfrastructure(services, section);

            var options = section.Get<StorageOptions>() ?? new StorageOptions();

            RegisterPendingStore(services, options.PendingProvider);
            RegisterAcceptedStore(services, options.AcceptedProvider);

            services.AddScoped<IFileStorage, FileStorageCoordinator>();

            return services;
        }

        // Registered regardless of AcceptedProvider: the whole point of the Stage 1
        // diagnostic is to prove the share works *before* switching the accepted store
        // onto it. Nothing here touches the network at boot — a session is only
        // established when an operation asks for one.
        private static void RegisterSmbInfrastructure(IServiceCollection services, IConfigurationSection section)
        {
            services.Configure<SmbOptions>(section.GetSection(SmbSectionName));

            // Singleton because the MaxConcurrentSessions semaphore is process-wide.
            services.AddSingleton<ISmbSessionFactory, SmbSessionFactory>();
            services.AddScoped<ISmbDiagnosticsService, SmbDiagnosticsService>();
        }

        private static void RegisterPendingStore(IServiceCollection services, string provider)
        {
            switch (provider)
            {
                case FileStorageProviders.Local:
                    services.AddScoped<IPendingFileStore, LocalPendingFileStore>();
                    break;

                case FileStorageProviders.Smb:
                    // Pending uploads stay local by design (spec/smb-file-storage.md, D1):
                    // they are short-lived, rewritten often, and gain nothing from the share.
                    throw new InvalidOperationException(
                        $"{SectionName}:PendingProvider '{FileStorageProviders.Smb}' is not supported — " +
                        "pending uploads are local by design.");

                default:
                    throw new InvalidOperationException(
                        $"Unknown {SectionName}:PendingProvider '{provider}'. Supported: {FileStorageProviders.Local}.");
            }
        }

        private static void RegisterAcceptedStore(IServiceCollection services, string provider)
        {
            switch (provider)
            {
                case FileStorageProviders.Local:
                    services.AddScoped<IAcceptedFileStore, LocalAcceptedFileStore>();
                    break;

                case FileStorageProviders.Smb:
                    throw new NotImplementedException(
                        $"{SectionName}:AcceptedProvider '{FileStorageProviders.Smb}' is not implemented yet");

                default:
                    throw new InvalidOperationException(
                        $"Unknown {SectionName}:AcceptedProvider '{provider}'. " +
                        $"Supported: {FileStorageProviders.Local}, {FileStorageProviders.Smb}.");
            }
        }
    }
}
