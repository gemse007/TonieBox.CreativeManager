using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TonieCreativeManager.Ui
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var filename = args.FirstOrDefault();
            if (filename == null || !System.IO.File.Exists(filename)) filename = ".env";
            if (!System.IO.File.Exists(filename)) filename = "../../.env";
            DotNetEnv.Env.Load(filename);

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
