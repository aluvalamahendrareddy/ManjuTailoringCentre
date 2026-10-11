namespace ManjuTailoringCentre.Services
{
    public interface IImageStorageService
    {
        Task<(string ImageUrl, string PublicId)> UploadImageAsync(IFormFile file);
        Task DeleteImageAsync(string publicId);
    }
}
