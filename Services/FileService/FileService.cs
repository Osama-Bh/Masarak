using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ECommerceApp.DTOs;
using GoWork.Data;
using GoWork.DTOs.FileDTOs;
using GoWork.Enums;
using Microsoft.EntityFrameworkCore;
using MimeKit.Cryptography;
using System.Text;
using UglyToad.PdfPig;

namespace GoWork.Services.FileService
{
    public class FileService : IFileService
    {
        private readonly BlobContainerClient _container;
        private readonly IConfiguration _config;
        private readonly StorageSharedKeyCredential _credential;

        public FileService(IConfiguration config)
        {
            _config = config;

            var account = _config["AzureBlob:StorageAccount"];
            var key = _config["AzureBlob:AccessKey"];
            var containerName = _config["AzureBlob:ContainerName"];

            _credential = new StorageSharedKeyCredential(account, key);

            var serviceClient = new BlobServiceClient(
                new Uri($"https://{account}.blob.core.windows.net"),
                _credential);

            _container = serviceClient.GetBlobContainerClient(containerName);
        }

        public async Task<FileUploadResultDto> UploadAsync(IFormFile file)
        {
            // 1️⃣ Validate file
            if (file == null || file.Length == 0)
                return null;
            // 2️⃣ Extract file info
            var fileName = file.FileName;
            var contentType = file.ContentType;
            using var stream = file.OpenReadStream();

            return await UploadAsync(stream, fileName, contentType);

        }
        // Accepted
        private async Task<FileUploadResultDto?> UploadAsync(Stream fileStream, string fileName, string contentType)
        {
            // 1️⃣ Validate file stream
            if (fileStream == null || !fileStream.CanRead)
                return null;

            // 2️⃣ Validate file name
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            // 3️⃣ Validate content type
            if (string.IsNullOrWhiteSpace(contentType))
                return null;

            // 4️⃣ Determine category from content type
            
            var category = _GetFileCategory(contentType);

            // 5️⃣ Business validation (size, allowed type)
            _ValidateFile(fileStream, contentType, category);

            try
            {
                // 6️⃣ Generate blob path
                var blobPath = _BuildBlobPath(fileName, category);

                // 7️⃣ Get blob client
                var blobClient = _container.GetBlobClient(blobPath);

                // 8️⃣ Reset stream position (VERY IMPORTANT)
                if (fileStream.CanSeek)
                    fileStream.Position = 0;

                // 9️⃣ Upload to Azure Blob with content type
                await blobClient.UploadAsync(
                    fileStream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        }
                    });

                var response = new FileUploadResultDto();

                // 10️⃣ Return result
                return new FileUploadResultDto
                {
                    BlobUri = blobClient.Uri.ToString(),
                    BlobName = fileName,
                    Category = category.ToString()
                };
            }
            catch (Azure.RequestFailedException)
            {
                return null;
            }
            catch
            {
                return null;
            }
        }


        public FileDownloadDto DownloadUrlAsync(string blobUri)
        {
            // 1️⃣ Validate input
            if (string.IsNullOrWhiteSpace(blobUri))
                return new FileDownloadDto();

            if (!Uri.TryCreate(blobUri, UriKind.Absolute, out var uri))
                return new FileDownloadDto();

            try
            {
                // 2️⃣ Get Blob Client
                var blobClient = new BlobClient(uri, _credential);

                // 3️⃣ Create SAS token
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = blobClient.BlobContainerName,
                    BlobName = blobClient.Name,
                    Resource = "b", // b = blob
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(4)
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                // 4️⃣ Generate SAS URI
                var sasUri = blobClient.GenerateSasUri(sasBuilder);

                // 5️⃣ Return DTO
                return new FileDownloadDto
                {
                    SasUrl = sasUri.ToString(),
                    ExpiresAt = sasBuilder.ExpiresOn,
                    Succeeded = true,
                };
            }
            catch (Azure.RequestFailedException)
            {
                return new FileDownloadDto();
            }
            catch
            {
                return new FileDownloadDto();
            }
        }

        // Edit the Implementation to return boolean
        public async Task<bool> UpdateAsync(IFormFile file, string blobUri)
        {
            // 1️⃣ Validate inputs
            if (file == null || file.Length == 0)
                return false;
            if (string.IsNullOrWhiteSpace(blobUri))
                return false;
            using var stream = file.OpenReadStream();
            return await _UpdateAsync(blobUri, stream, file.ContentType);
        }

        public async Task<bool> DeleteAsync(string blobUri)
        {
            if (string.IsNullOrWhiteSpace(blobUri))
                return false;

            if (!Uri.TryCreate(blobUri, UriKind.Absolute, out var uri))
                return false;

            try
            {
                var blobClient = new BlobClient(uri, _credential);

                var response = await blobClient.DeleteIfExistsAsync();

                return response.Value; // true = deleted, false = not found
            }
            catch (Azure.RequestFailedException)
            {
                // log exception if needed
                return false;
            }
            catch
            {
                return false;
            }
        }

        // make this return boolean
        private async Task<bool> _UpdateAsync(string blobUri, Stream fileStream, string contentType)
        {
            // 1️⃣ Validate blob URI
            if (string.IsNullOrWhiteSpace(blobUri))
                return false;

            if (!Uri.TryCreate(blobUri, UriKind.Absolute, out var uri))
                return false;

            // 2️⃣ Validate file inputs
            if (fileStream == null || !fileStream.CanRead)
                return false;

            if (string.IsNullOrWhiteSpace(contentType))
                return false;

            var category = _GetFileCategory(contentType);

            // 3️⃣ Custom business validation
            _ValidateFile(fileStream, contentType, category);

            try
            {
                // 4️⃣ Get existing blob client
                var blobClient = new BlobClient(uri, _credential);

                // 5️⃣ Reset stream position (VERY important)
                if (fileStream.CanSeek)
                    fileStream.Position = 0;

                // 6️⃣ Upload & overwrite
                await blobClient.UploadAsync(
                    fileStream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        }
                    });

                return true;
            }
            catch (Azure.RequestFailedException)
            {
                // log if needed
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void _ValidateFile(Stream stream, string contentType, FileCategoryEnum category)
        {
            var maxSizeMb = int.Parse(_config["AzureBlob:MaxFileSizeMB"]);
            var maxBytes = maxSizeMb * 1024 * 1024;

            if (stream.Length > maxBytes)
                throw new Exception($"File exceeds {maxSizeMb}MB");

            var allowedTypes = category == FileCategoryEnum.ProfilePIcture
                ? _config.GetSection("AzureBlob:AllowedImages").Get<string[]>()
                : _config.GetSection("AzureBlob:AllowedResumes").Get<string[]>();

            if (!allowedTypes.Contains(contentType))
                throw new Exception("File type not allowed");
        }

        private string _BuildBlobPath(string fileName, FileCategoryEnum category)
        {
            var extension = Path.GetExtension(fileName);
            var folder = category == FileCategoryEnum.ProfilePIcture ? "images" : "resumes";

            return $"{folder}/{Guid.NewGuid()}{extension}";
        }

        private FileCategoryEnum _GetFileCategory(string contentType)
        {
            return contentType switch
            {
                "image/jpeg" => FileCategoryEnum.ProfilePIcture,
                "image/png" => FileCategoryEnum.ProfilePIcture,
                "image/webp" => FileCategoryEnum.ProfilePIcture,

                "application/pdf" => FileCategoryEnum.Resume,

                _ => throw new Exception($"Unsupported content type: {contentType}")
            };
        }

        public async Task<string> ExtractPdfTextAsync(string pdfUrl)
        {
            using var httpClient = new HttpClient();

            var pdfBytes = await httpClient.GetByteArrayAsync(pdfUrl);

            using var stream = new MemoryStream(pdfBytes);

            var text = new StringBuilder();

            using (var document = PdfDocument.Open(stream))
            {
                foreach (var page in document.GetPages())
                {
                    text.AppendLine(page.Text);
                }
            }

            return text.ToString();
        }
    }
}
