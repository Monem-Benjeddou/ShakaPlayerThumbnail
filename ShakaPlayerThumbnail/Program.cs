using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using ShakaPlayerThumbnail.BackgroundServices;
using ShakaPlayerThumbnail.Hubs;
using ShakaPlayerThumbnail.Repository;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", builder =>
    {
        builder.WithOrigins("https:thumbnail.john-group.org")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); 
    });
});
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddSingleton<IProgressTracker, InMemoryProgressTracker>();
builder.Services.AddSingleton<IVideoRepository, VideoRepository>();
builder.Services.AddHostedService<ThumbnailGenerationService>();
builder.Services.AddSignalR();

var app = builder.Build();

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
app.UseCors("AllowSpecificOrigins");

app.UseAuthorization();
app.MapHub<UploadProgressHub>("/uploadProgressHub");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Video}/{action=ListVideos}/{id?}");
app.UseStaticFiles(new StaticFileOptions
{
  FileProvider = new PhysicalFileProvider("/etc/data"),
  RequestPath = "/data",
  ContentTypeProvider = provider
});
ThreadPool.SetMinThreads(200, 200);
app.Run();