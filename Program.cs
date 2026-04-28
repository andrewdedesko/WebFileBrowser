using Microsoft.AspNetCore.Authentication.Cookies;
using WebFileBrowser.Configuration;
using WebFileBrowser.Services;
using WebFileBrowser.Services.ObjectDetection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<ShareMapping>();
builder.Services.AddSingleton<UserCredentials>();
builder.Services.AddSingleton<DefaultViews>();

builder.Services.AddSingleton<IUserAuthenticationService, UserAuthenticationService>();
builder.Services.AddSingleton<IShareService, ShareService>();
builder.Services.AddSingleton<FileSystemBrowseService>();
builder.Services.AddSingleton<IBrowseService>(ctx => ctx.GetRequiredService<FileSystemBrowseService>());
builder.Services.AddSingleton<IFileTypeService, FileTypeService>();

builder.Services.AddSingleton<ImageLoader>();
builder.Services.AddSingleton<IObjectDetectionService, ObjectDetectionService>();
builder.Services.AddSingleton<BackgroundThumbnailQueue>(ctx => {
    return new BackgroundThumbnailQueue(1000);
});
builder.Services.AddSingleton<ImageThumbnailer>();
builder.Services.AddSingleton<VideoThumbnailer>();
builder.Services.AddSingleton<DirectoryThumbnailer>();
builder.Services.AddSingleton<ThumbnailAutoCropper>();
builder.Services.AddSingleton<IAutoCropper, FaceSquareAutoCropper>();
builder.Services.AddSingleton<IImageThumbnailService, ImageThumbnailService>();
builder.Services.AddSingleton<ThumbnailBackgroundProcessingService>();
builder.Services.AddHostedService(ctx => ctx.GetRequiredService<ThumbnailBackgroundProcessingService>());

var thumbnailPreCachingEnabled = builder.Configuration.GetSection("Thumbnails")?.GetValue<bool>("PreCache") ?? false;
if(thumbnailPreCachingEnabled){
    builder.Services.AddSingleton<ThumbnailPreCacheBackgroundService>();
    builder.Services.AddHostedService(ctx => ctx.GetRequiredService<ThumbnailPreCacheBackgroundService>());
}

builder.Services.AddSingleton<IObjectDetector, SharpAiFaceDetector>();
builder.Services.AddSingleton<IObjectDetector, YoloCocoObjectDetector>();

var objectDetectionModels = builder.Configuration.GetSection("ObjectDetectionModels")?.Get<IEnumerable<string>>();
if(objectDetectionModels != null) {
    var objectDetectorLoader = new ObjectDetectorLoader();
    foreach(var objectDetectionModel in objectDetectionModels) {
        builder.Services.AddSingleton(ctx => objectDetectorLoader.LoadObjectDetector(objectDetectionModel, ctx.GetService<ILogger<OnnxObjectDetector>>()));
    }
}

var cacheType = builder.Configuration.GetSection("Caching")?.GetValue<string>("CacheType")?.ToLower();
if(cacheType == "redis") {
    builder.Services.AddStackExchangeRedisCache(options =>
     {
         options.Configuration = builder.Configuration.GetConnectionString("RedisCache");
         options.InstanceName = "WebFileViewerCache";
     });
}else{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.AccessDeniedPath = "/Forbidden/";
    });

builder.Services.AddControllers(options =>
{
    options.CacheProfiles.Add("Media", new Microsoft.AspNetCore.Mvc.CacheProfile()
    {
        Duration = 4800
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

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
