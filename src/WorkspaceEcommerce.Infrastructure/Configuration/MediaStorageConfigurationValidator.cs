using Microsoft.Extensions.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Configuration;

public static class MediaStorageConfigurationValidator
{
    public static MediaStorageOptions GetValidatedMediaStorageOptions(
        this IConfiguration configuration,
        string environmentName)
    {
        var options = configuration.GetSection(MediaStorageOptions.SectionName).Get<MediaStorageOptions>()
            ?? new MediaStorageOptions();
        var provider = options.Provider.Trim();

        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
            !Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicBaseUri) ||
            (publicBaseUri.Scheme != Uri.UriSchemeHttp && publicBaseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("MediaStorage:PublicBaseUrl must be an absolute HTTP(S) URL controlled by the deployment.");
        }

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(publicBaseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("MediaStorage:PublicBaseUrl must use HTTPS outside Development.");
        }

        if (options.MaxUploadBytes is <= 0 or > 25 * 1024 * 1024 ||
            options.MaxWidth <= 0 || options.MaxHeight <= 0 || options.MaxPixels <= 0 ||
            options.CleanupRetentionHours < 1)
        {
            throw new InvalidOperationException("MediaStorage limits must be positive and the upload limit must not exceed 25 MB.");
        }

        if (string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("MediaStorage:Provider=Local is permitted only in Development. Configure the S3 provider outside Development.");
            }

            return options;
        }

        if (!string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("MediaStorage:Provider must be either Local or S3.");
        }

        if (string.IsNullOrWhiteSpace(options.Bucket) || string.IsNullOrWhiteSpace(options.ServiceUrl) ||
            string.IsNullOrWhiteSpace(options.Region) || string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey) ||
            ConfigurationPlaceholders.ContainsPlaceholder(options.AccessKey) ||
            ConfigurationPlaceholders.ContainsPlaceholder(options.SecretKey))
        {
            throw new InvalidOperationException("S3 media storage requires Bucket, ServiceUrl, Region, AccessKey, and SecretKey from a secret store.");
        }

        return options;
    }
}
