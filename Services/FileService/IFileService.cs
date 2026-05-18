using ECommerceApp.DTOs;
using GoWork.DTOs.FileDTOs;
using GoWork.Enums;

namespace GoWork.Services.FileService
{
    public interface IFileService
    {
        Task<FileUploadResultDto?> UploadAsync(IFormFile file);


        FileDownloadDto DownloadUrlAsync(string blobUri);

        Task<bool> DeleteAsync(string blobUri);

        Task<bool> UpdateAsync(IFormFile file, string blobUri);

        Task<string> ExtractPdfTextAsync(string pdfUrl);

    }
}
