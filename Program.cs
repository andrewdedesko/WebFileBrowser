using Microsoft.AspNetCore.Authentication.Cookies;
using WebFileBrowser.Configuration;
using WebFileBrowser.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ShareMapping>();
builder.Services.AddSingleton<UserCredentials>();
builder.Services.AddSingleton<DefaultViews>();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<IUserAuthenticationService, UserAuthenticationService>();
builder.Services.AddSingleton<IShareService, ShareService>();
builder.Services.AddSingleton<IBrowseService, BrowseService>();

// builder.Services.AddDistributedMemoryCache();
builder.Services.AddStackExchangeRedisCache(options =>
 {
     options.Configuration = builder.Configuration.GetConnectionString("RedisCache");
     options.InstanceName = "WebFileViewerCache";
 });

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
