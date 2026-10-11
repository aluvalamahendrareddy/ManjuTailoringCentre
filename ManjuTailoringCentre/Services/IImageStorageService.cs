namespace ManjuTailoringCentre.Services
{
    public interface IImageStorageService
    {
        Task<(string ImageUrl, string PublicId)> UploadImageAsync(IFormFile file, string? category = null);
        Task DeleteImageAsync(string publicId);
        Task UpdateImageCategoryAsync(string publicId, string newCategory);
        Task<List<(string ImageUrl, string PublicId, string Category)>> FetchAllImagesFromCloudinaryAsync();
    }
}
