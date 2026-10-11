using System.ComponentModel.DataAnnotations;

namespace ManjuTailoringCentre.Models
{
    public class GalleryPhoto
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Category { get; set; } = "General";

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        // Cloudinary public ID — needed to delete image from Cloudinary
        public string CloudinaryPublicId { get; set; } = string.Empty;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
