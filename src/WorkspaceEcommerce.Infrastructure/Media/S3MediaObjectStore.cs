using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Media;

internal sealed class S3MediaObjectStore : IMediaObjectStore
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3MediaObjectStore(MediaStorageOptions options)
    {
        var configuration = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), configuration);
        _bucket = options.Bucket!;
    }

    public async Task PutAsync(string objectKey, ReadOnlyMemory<byte> content, string contentType, CancellationToken cancellationToken)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = objectKey,
            InputStream = new MemoryStream(content.ToArray(), writable: false),
            ContentType = contentType,
            Headers = { ContentDisposition = "inline" }
        }, cancellationToken);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        await _client.DeleteObjectAsync(_bucket, objectKey, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, objectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
