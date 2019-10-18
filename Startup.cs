using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkiaSharp;

namespace STCore
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        static SQLiteConnection cn;

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles(); // for index.html
            app.UseStaticFiles(); // For the wwwroot folder
            app.UseRouting();

            cn = new SQLiteConnection($@"Data Source={env.ContentRootPath}\App_Data\db.sqlite;Version=3;");
            cn.Open();
            SpatialiteLoader.Load(cn);

            var routeBuilder = new RouteBuilder(app);

            routeBuilder.MapGet("/tile/{z}/{x}/{y}", async context =>
            {
                await TileHandler.RenderTile(context, cn);
            });

            var routes = routeBuilder.Build();
            app.UseRouter(routes);
        }
    }
}

