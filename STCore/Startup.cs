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

            app.UseStaticFiles(); // For the wwwroot folder
            app.UseRouting();

            cn = new SQLiteConnection($@"Data Source={env.ContentRootPath}\App_Data\db.sqlite;Version=3;");
            cn.Open();
            SpatialiteLoader.Load(cn);

            var routeBuilder = new RouteBuilder(app);

            routeBuilder.MapGet("/tile/{z}/{x}/{y}", async context =>
            {
                uint x = uint.Parse(context.GetRouteValue("x").ToString());
                uint y = uint.Parse(context.GetRouteValue("y").ToString());
                uint z = uint.Parse(context.GetRouteValue("z").ToString());

                // calc rect from tile key
                var qw = TransformTools.TileToWgs(x, y, z);

                // build the sql
                var query = FormattableString.Invariant($@"
                    SELECT Id, AsBinary(Geometry) FROM WorldGeom 
                        WHERE ROWID IN 
                            (Select rowid FROM cache_WorldGeom_Geometry 
                                WHERE mbr = FilterMbrIntersects({qw.Left}, {qw.Bottom}, {qw.Right}, {qw.Top}))
                    ");

                var ii = new SKImageInfo
                {
                    Width = 256,
                    Height = 256,
                    ColorType = SKImageInfo.PlatformColorType,
                    AlphaType = SKAlphaType.Premul
                };

                using (var command = new SQLiteCommand(query, cn))
                using (var reader = await command.ExecuteReaderAsync())
                using (var surface = SKSurface.Create(ii))
                using (var paint = new SKPaint())
                {
                    //paint.IsAntialias = true;
                    paint.Color = new SKColor(0x2c, 0x3e, 0x50);                   
                    SKCanvas canvas = surface.Canvas;

                    while (await reader.ReadAsync())
                    {
                        int id = reader.GetInt32(0);
                        byte[] wkb = reader[1] as byte[];

                        // create GDI path from wkb
                        var path = WkbToGdi.Parse(wkb, p => TransformTools.WgsToTile(x, y, z, p));

                        // degenerated polygon
                        if (path == null)
                            continue;

                        canvas.DrawPath(path, paint);
                    }

                    var m_skImage = surface.Snapshot();
                    var data = m_skImage.Encode(SKEncodedImageFormat.Png, 80);
                    var ResultImageStream = data.AsStream();
                    var imageStream = new MemoryStream();
                    await ResultImageStream.CopyToAsync(imageStream);
                    var ResultData = imageStream.ToArray();

                    context.Response.ContentType = "image/PNG";
                    await context.Response.Body.WriteAsync(ResultData);

                }

            });

            var routes = routeBuilder.Build();
            app.UseRouter(routes);
        }
    }
}

