using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using VepayDO.Application.Configuration;
using VepayDO.Application.Storage;
using VepayDO.Domain.Time;
using VepayDO.Infrastructure.Storage.InMemory;
using VepayDO.Infrastructure.Storage.S3;

namespace VepayDO.Infrastructure.Storage;

public static class StorageDependencyInjection
{
    public static IServiceCollection AddStorage(this IServiceCollection services, AppConfig config)
    {
        if (string.Equals(config.StorageProvider, StorageProviders.S3, StringComparison.OrdinalIgnoreCase))
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = config.S3Endpoint,
                ForcePathStyle = config.S3ForcePathStyle,
                AuthenticationRegion = config.S3Region,
            };

            services.AddSingleton<IAmazonS3>(_ => string.IsNullOrWhiteSpace(config.S3AccessKey)
                ? new AmazonS3Client(s3Config)
                : new AmazonS3Client(config.S3AccessKey, config.S3SecretKey, s3Config));

            services.AddSingleton<IStorageToolkitFactory>(
                sp => new S3StorageToolkitFactory(
                    sp.GetRequiredService<IAmazonS3>(),
                    config.S3Bucket ?? string.Empty));
        }
        else
        {
            services.AddSingleton(_ => new StorageUrlSigner(config.StorageSigningSecret));
            services.AddSingleton<IStorageToolkitFactory>(
                sp => new InMemoryStorageToolkitFactory(
                    sp.GetRequiredService<StorageUrlSigner>(),
                    config.StoragePublicBaseUrl,
                    sp.GetRequiredService<IClock>()));
        }

        services.AddSingleton<IStorageToolkit>(sp => sp.GetRequiredService<IStorageToolkitFactory>().Create());
        return services;
    }
}
