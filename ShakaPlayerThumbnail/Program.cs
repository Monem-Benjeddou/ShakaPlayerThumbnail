using Microsoft.AspNetCore.StaticFiles;
using ShakaPlayerThumbnail.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".vtt"] = "text/vtt";
provider.Mappings[".mp4"] = "video/mp4";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});


app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();