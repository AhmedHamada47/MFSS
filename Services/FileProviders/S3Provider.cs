using Amazon.S3;
using Amazon.S3.Model;
using MFSS.Models;

namespace MFSS.Services.FileProviders;

public class S3Provider : IFileProvider
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly string _publicUrl;

    public S3Provider(FileSystemConfig c)
    {
        _bucket = c.BucketName;
        _publicUrl = c.PublicUrl.TrimEnd('/');
        _client = new AmazonS3Client(c.AccessKey, c.SecretKey, new AmazonS3Config { ServiceURL = c.Endpoint, ForcePathStyle = true });
    }

    public async Task<(string url, long size)> UploadAsync(Stream s, string key, string ct)
    {
        await _client.PutObjectAsync(new PutObjectRequest { BucketName = _bucket, Key = key, InputStream = s, ContentType = ct, DisablePayloadSigning = true });
        return (GetPublicUrl(key), s.Length);
    }

    public async Task<Stream> DownloadAsync(string src, string key) => (await _client.GetObjectAsync(_bucket, key)).ResponseStream;
    public async Task DeleteAsync(string url) => await _client.DeleteObjectAsync(_bucket, url.Replace($"{_publicUrl}/", ""));
    public string GetPublicUrl(string key) => $"{_publicUrl}/{key}";
}
