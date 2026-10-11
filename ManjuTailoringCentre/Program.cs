using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using ManjuTailoringCentre.Data;
using ManjuTailoringCentre.Models;
using ManjuTailoringCentre.Services;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add MVC
builder.Services.AddControllersWithViews();

// SQLite database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=gallery.db"));

// Image storage service (Local disk when no keys provided, Cloudinary when keys are provided)
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();

// Cookie authentication for Admin login
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Admin/Login";
        options.LogoutPath = "/Admin/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Auto-create and seed SQLite database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var defaults = new[] { "Designer Blouse", "Normal Blouse", "Crop Top", "Kids Frocks & Dresses", "Saree kongu mudulu", "Chudidhar & Salwar", "Long Gowns & Frocks", "Custom Dresses", "Alterations" };

    // 1. Seed Categories if empty
    if (!db.Categories.Any())
    {
        foreach (var d in defaults)
        {
            db.Categories.Add(new CategoryItem { Name = d });
        }
        db.SaveChanges();
    }

    // 2. Seed Gallery Photos from Cloudinary or embedded gallery_seed.json if empty
    var storage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
    if (!db.GalleryPhotos.Any())
    {
        bool seededFromCloud = false;
        try
        {
            var cloudPhotos = storage.FetchAllImagesFromCloudinaryAsync().GetAwaiter().GetResult();
            if (cloudPhotos != null && cloudPhotos.Count > 0)
            {
                var knownCategories = new HashSet<string>(db.Categories.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var cp in cloudPhotos)
                {
                    var cat = !string.IsNullOrWhiteSpace(cp.Category) ? cp.Category.Trim() : "Designer Blouse";
                    if (cat.Equals("Pattu Langa / Half Saree", StringComparison.OrdinalIgnoreCase) || 
                        cat.Equals("Saree Kongu Muddillu", StringComparison.OrdinalIgnoreCase))
                    {
                        cat = "Saree kongu mudulu";
                    }

                    db.GalleryPhotos.Add(new GalleryPhoto
                    {
                        Title = cat,
                        Category = cat,
                        ImageUrl = cp.ImageUrl,
                        CloudinaryPublicId = cp.PublicId,
                        UploadedAt = DateTime.UtcNow
                    });

                    if (knownCategories.Add(cat))
                    {
                        db.Categories.Add(new CategoryItem { Name = cat });
                    }
                }
                db.SaveChanges();
                seededFromCloud = true;
            }
        }
        catch { }

        if (!seededFromCloud)
        {
            var seedPath = Path.Combine(app.Environment.ContentRootPath, "Data", "gallery_seed.json");
            if (File.Exists(seedPath))
            {
                try
                {
                    var json = File.ReadAllText(seedPath);
                    var items = JsonSerializer.Deserialize<List<GallerySeedItem>>(json);
                    if (items != null && items.Count > 0)
                    {
                        foreach (var it in items)
                        {
                            var cat = it.Category;
                            if (cat.Equals("Pattu Langa / Half Saree", StringComparison.OrdinalIgnoreCase) || 
                                cat.Equals("Saree Kongu Muddillu", StringComparison.OrdinalIgnoreCase))
                            {
                                cat = "Saree kongu mudulu";
                            }
                            db.GalleryPhotos.Add(new GalleryPhoto
                            {
                                Title = cat,
                                Category = cat,
                                ImageUrl = it.ImageUrl,
                                CloudinaryPublicId = it.PublicId,
                                UploadedAt = DateTime.UtcNow
                            });
                        }
                        db.SaveChanges();
                    }
                }
                catch { }
            }
        }
    }

    // 3. Migrate any existing photos with old category names
    var photosToMigrate = db.GalleryPhotos
        .Where(p => p.Category == "Pattu Langa / Half Saree" || p.Category == "Saree Kongu Muddillu")
        .ToList();
    if (photosToMigrate.Any())
    {
        foreach (var p in photosToMigrate)
        {
            p.Category = "Saree kongu mudulu";
            p.Title = "Saree kongu mudulu";
        }
        db.SaveChanges();
    }

    // 4. De-duplicate Categories table and ensure Crop Top & Saree kongu mudulu exist
    var allCats = db.Categories.ToList();
    var seenCatNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var duplicateCats = new List<CategoryItem>();
    foreach (var c in allCats)
    {
        var trimmed = c.Name.Trim();
        c.Name = trimmed;
        // Merge old variations
        if (trimmed.Equals("Pattu Langa / Half Saree", StringComparison.OrdinalIgnoreCase) || 
            trimmed.Equals("Saree Kongu Muddillu", StringComparison.OrdinalIgnoreCase))
        {
            c.Name = "Saree kongu mudulu";
            trimmed = "Saree kongu mudulu";
        }

        if (!seenCatNames.Add(trimmed))
        {
            duplicateCats.Add(c);
        }
    }
    if (duplicateCats.Any())
    {
        db.Categories.RemoveRange(duplicateCats);
        db.SaveChanges();
    }

    // Ensure required core categories exist
    if (!seenCatNames.Contains("Crop Top"))
    {
        db.Categories.Add(new CategoryItem { Name = "Crop Top" });
        db.SaveChanges();
    }
    if (!seenCatNames.Contains("Saree kongu mudulu"))
    {
        db.Categories.Add(new CategoryItem { Name = "Saree kongu mudulu" });
        db.SaveChanges();
    }

    // 5. Ensure Cloudinary tags for saree images are updated
    _ = Task.Run(async () =>
    {
        try
        {
            var sareePids = new[]
            {
                "manju-tailoring/gallery/fax84lssjglanw7vzle5",
                "manju-tailoring/gallery/fiedoapvmtq6p454pg4b",
                "manju-tailoring/gallery/kn81ce6hkxryjktphdh0",
                "manju-tailoring/gallery/rhsyssxhuchnueuimo0h",
                "manju-tailoring/gallery/trspeck6ofgpaovjxuet",
                "manju-tailoring/gallery/uq73bl2zskvlaqvwzitg",
                "manju-tailoring/gallery/v1vxnyar2dpf6lydxraw",
                "manju-tailoring/gallery/xetl8ugizaimvrbu52yj",
                "manju-tailoring/gallery/yqhtu73euagl25vfhvr2"
            };
            foreach (var pid in sareePids)
            {
                await storage.UpdateImageCategoryAsync(pid, "Saree kongu mudulu");
            }
        }
        catch { }
    });
}

// Configure HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
