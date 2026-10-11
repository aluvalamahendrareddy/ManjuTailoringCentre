namespace ManjuTailoringCentre.Services
{
    public interface IImageStorageService
    {
        Task<(string ImageUrl, string PublicId)> UploadImageAsync(IFormFile file);
        Task DeleteImageAsync(string publicId);
        Task<List<(string ImageUrl, string PublicId, string Category)>> FetchAllImagesFromCloudinaryAsync();
    }
}
