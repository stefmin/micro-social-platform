using Amazon.S3;
using MicroSocialPlatform.Data;
using MicroSocialPlatform.Models;
using MicroSocialPlatform.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MicroSocialPlatform.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<ICursorPagingService, CursorPagingService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IContentModerator, OpenAIModeratorService>();

// add S3 connection initialization
var s3Options = builder.Configuration.GetSection("S3").Get<AmazonS3Config>();
if (s3Options != null)
{
    s3Options.ForcePathStyle = true;

    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client(
        builder.Configuration["S3:AccessKey"],
        builder.Configuration["S3:SecretKey"],
        s3Options
    ));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    SeedData.Initialize(services);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.UseBannedUserCheck();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

//app.MapControllerRoute(
//    name: "post",
//    pattern: "{controller=Post}/{action=Index}/{id?}")
//    .WithStaticAssets();

app.MapControllerRoute(
    name: "post_actions",
    pattern: "Post/{action}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

//await DebugReset.Run(app);

app.Run();
