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

    // 1. Seed Categories if empty
    if (!db.Categories.Any())
    {
        var defaults = new[] { "Designer Blouse", "Normal Blouse", "Kids Frocks & Dresses", "Saree Kongu Muddillu", "Chudidhar & Salwar", "Long Gowns & Frocks", "Custom Dresses", "Alterations" };
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
                foreach (var cp in cloudPhotos)
                {
                    var cat = !string.IsNullOrWhiteSpace(cp.Category) ? cp.Category : "Designer Blouse";
                    if (cat == "Pattu Langa / Half Saree") cat = "Saree Kongu Muddillu";

                    db.GalleryPhotos.Add(new GalleryPhoto
                    {
                        Title = cat,
                        Category = cat,
                        ImageUrl = cp.ImageUrl,
                        CloudinaryPublicId = cp.PublicId,
                        UploadedAt = DateTime.UtcNow
                    });

                    if (!db.Categories.Any(c => c.Name == cat))
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
                            var cat = it.Category == "Pattu Langa / Half Saree" ? "Saree Kongu Muddillu" : it.Category;
                            var title = it.Title == "Pattu Langa / Half Saree" ? "Saree Kongu Muddillu" : it.Title;
                            db.GalleryPhotos.Add(new GalleryPhoto
                            {
                                Title = title,
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

    // 3. Migrate any existing photos/categories with the old name
    var oldCatPhotos = db.GalleryPhotos.Where(p => p.Category == "Pattu Langa / Half Saree").ToList();
    if (oldCatPhotos.Any())
    {
        foreach (var p in oldCatPhotos)
        {
            p.Category = "Saree Kongu Muddillu";
            p.Title = "Saree Kongu Muddillu";
        }
        db.SaveChanges();
    }
    var oldCategoryItem = db.Categories.FirstOrDefault(c => c.Name == "Pattu Langa / Half Saree");
    if (oldCategoryItem != null)
    {
        oldCategoryItem.Name = "Saree Kongu Muddillu";
        db.SaveChanges();
    }
    if (!db.Categories.Any(c => c.Name == "Saree Kongu Muddillu"))
    {
        db.Categories.Add(new CategoryItem { Name = "Saree Kongu Muddillu" });
        db.SaveChanges();
    }

    // 4. Ensure Cloudinary tags for saree images are updated
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
                await storage.UpdateImageCategoryAsync(pid, "Saree Kongu Muddillu");
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
