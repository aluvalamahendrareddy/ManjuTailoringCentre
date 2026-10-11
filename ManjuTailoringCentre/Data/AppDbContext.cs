using Microsoft.EntityFrameworkCore;
using ManjuTailoringCentre.Models;

namespace ManjuTailoringCentre.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<GalleryPhoto> GalleryPhotos { get; set; }
    }
}
