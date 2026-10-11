using System.ComponentModel.DataAnnotations;

namespace ManjuTailoringCentre.Models
{
    public class CategoryItem
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public int DisplayOrder { get; set; } = 0;
    }
}
