using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.JSInterop;
using MudBlazor.Services;
using System.Runtime.InteropServices;
using TonieCloud;
using TonieCreativeManager.Service;
using TonieCreativeManager.Service.Model;
using TonieCreativeManager.UI2.Shared;

namespace TonieCreativeManager.UI2
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var options = new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = WindowsServiceHelpers.IsWindowsService()
                                     ? AppContext.BaseDirectory : default
            };
            var builder = WebApplication.CreateBuilder(options);
            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddHostedService<TonieBoxService>();
            builder.Services.AddServerSideBlazor();
            builder.Services
                .AddMudServices()
                .AddSingleton<RepositoryService>()
                .AddSingleton(builder.Configuration.GetSection("Settings")?.Get<Settings>() ?? new Settings())
                .AddSingleton(s =>
                {
                    var repositoryService = s.GetService<RepositoryService>();
                    var gs = repositoryService?.GetGeneralSettings()?.Result;
                    var login = new Login(gs?.TonieUserID ?? "", gs?.ToniePassword ?? "");
                    return new TonieCloudClient(login, s.GetService<Settings>()?.MaxParallelFileUploades ?? 1);
                })
                .AddSingleton<TonieCloudService>()
                .AddSingleton<MediaService>()
                .AddSingleton<UploadService>()
                .AddSingleton<VoucherService>()
                .AddSingleton<UserService>()
                .AddScoped<TokenProvider>()
                .AddScoped<IEnumerable<PersistentData.User>>(s => s.GetService<RepositoryService>()?.GetUsers()?.Result ?? new PersistentData.User[] { })
                .AddScoped<PersistentData.User>(s => s.GetService<IEnumerable<PersistentData.User>>()?.FirstOrDefault(_ => s.GetService<TokenProvider>()?.SelectedUser == _.Id) ?? new PersistentData.User())
                ;

            builder.Host.UseWindowsService();

            var app = builder.Build();
            Task.Run(async () =>
            {
                var lib = Environment.GetEnvironmentVariable("MEDIA_LIBRARY");
                var cfg = Environment.GetEnvironmentVariable("MEDIA_DATAFILE");
                Console.WriteLine($"MEDIA_LIBRARY: {lib}");
                Console.WriteLine($"MEDIA_DATAFILE: {lib}");
                var settings = app.Services.GetService<Settings>() ?? new Settings();
                if (!string.IsNullOrEmpty(lib))
                {
                    settings.LibraryRoot = lib;
                }
                settings.LibraryRoot = settings.LibraryRoot?.TrimEnd('\\').TrimEnd('/');
                if (settings.LibraryRoot != null)
                {
                    if (!string.IsNullOrEmpty(cfg))
                        settings.RepositoryDataFile = cfg;
                    else
                        settings.RepositoryDataFile = Path.Combine(settings.LibraryRoot, settings.RepositoryDataFile ?? "data.json");
                    Console.WriteLine($"Using LibraryRoot: {settings.LibraryRoot}");
                    Console.WriteLine($"Using Repository: {settings.RepositoryDataFile}");
                    await (app.Services.GetService<MediaService>()?.GetMediaItemAsync("") ?? Task.CompletedTask);
                }
            });
            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();
            
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");
            app.Run();
        }
    }
}