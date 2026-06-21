using MFSS.Models;

namespace MFSS.Services.FileProviders;

public static class FileProviderFactory
{
    public static IFileProvider Create(FileSystemConfig c) => c.Type.ToLower() switch
    {
        "cloudflarer2" or "r2" or "s3" or "aws" or "minio" => new S3Provider(c),
        "local" or "folder" => new LocalProvider(c),
        "url" or "http" or "cloudinary" => new UrlDownloader(),
        _ => throw new Exception($"Unknown type: '{c.Type}'")
    };
}
