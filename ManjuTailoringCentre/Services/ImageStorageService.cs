using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ManjuTailoringCentre.Services
{
    public class ImageStorageService : IImageStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly Cloudinary? _cloudinary;
        private readonly bool _useCloudinary;

        public ImageStorageService(IConfiguration config, IWebHostEnvironment env)
        {
            _env = env;

            var cloudName = config["Cloudinary:CloudName"];
            var apiKey = config["Cloudinary:ApiKey"];
            var apiSecret = config["Cloudinary:ApiSecret"];

            // If Cloudinary keys are configured, use Cloudinary
            if (!string.IsNullOrWhiteSpace(cloudName) && 
                !string.IsNullOrWhiteSpace(apiKey) && 
                !string.IsNullOrWhiteSpace(apiSecret) &&
                cloudName != "YOUR_CLOUD_NAME")
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
                _useCloudinary = true;
            }
            else
            {
                // Fallback to local storage (wwwroot/uploads) - works out-of-the-box with NO keys needed!
                _useCloudinary = false;
            }
        }

        public async Task<(string ImageUrl, string PublicId)> UploadImageAsync(IFormFile file)
        {
            if (_useCloudinary && _cloudinary != null)
            {
                using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "manju-tailoring/gallery",
                    Transformation = new Transformation()
                        .Width(1200).Height(1200)
                        .Crop("limit")
                        .Quality("auto")
                        .FetchFormat("auto")
                };

                var result = await _cloudinary.UploadAsync(uploadParams);
                if (result.Error != null)
                    throw new Exception($"Cloudinary upload failed: {result.Error.Message}");

                return (result.SecureUrl.ToString(), result.PublicId);
            }
            else
            {
                // Local disk storage in wwwroot/uploads
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                return ($"/uploads/{uniqueFileName}", uniqueFileName);
            }
        }

        public async Task DeleteImageAsync(string publicId)
        {
            if (_useCloudinary && _cloudinary != null)
            {
                var deleteParams = new DeletionParams(publicId);
                await _cloudinary.DestroyAsync(deleteParams);
            }
            else
            {
                var filePath = Path.Combine(_env.WebRootPath, "uploads", publicId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                await Task.CompletedTask;
            }
        }
    }
}
