using System;
using System.IO;
using System.Collections.Generic;
using System.Data.SQLite;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;
using SkiaSharp;

namespace STCore
{
    public class TileHandler
    {
        public static async Task RenderTile(HttpContext context, SQLiteConnection cn)
        {
            uint x = uint.Parse(context.GetRouteValue("x").ToString());
            uint y = uint.Parse(context.GetRouteValue("y").ToString());
            uint z = uint.Parse(context.GetRouteValue("z").ToString());

            // calc rect from tile key
            var qw = TransformTools.TileToWgs(x, y, z);

            // build the sql
            var query = FormattableString.Invariant($@"
                    SELECT WorldGeom.Id, AsBinary(Geometry), Pop/Area as PopDens FROM WorldGeom 
                    JOIN WorldData on WorldData.Id = WorldGeom.Id 
                    WHERE WorldGeom.ROWID IN 
                        (Select rowid FROM cache_WorldGeom_Geometry 
                        WHERE mbr = FilterMbrIntersects({qw.Left}, {qw.Bottom}, {qw.Right}, {qw.Top}))
                    ");

            var choropleth = new Classification<double, SKColor>
            {
                MinValue = 0, // lower border for classification
                DefaultAttribute = SKColors.White, // color if key hits no class
                Values = new SortedList<double, SKColor> { // the classes
                        { 50, SKColors.Green },
                        { 100, SKColors.LightGreen },
                        { 250, SKColors.Yellow },
                        { 500,SKColors.Orange },
                        { 1000, SKColors.Red },
                        { 2500, SKColors.DarkRed },
                        { double.MaxValue, SKColors.Purple }
                    }
            };

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
            {
                var paint = new SKPaint{
                    Style = SKPaintStyle.Fill
                };

                var stokePaint = new SKPaint
                {
                    IsAntialias = true,
                    StrokeJoin = SKStrokeJoin.Round,
                    StrokeWidth = z,
                    Color = SKColors.Black,
                    Style = SKPaintStyle.Stroke
                };

                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    byte[] wkb = reader[1] as byte[];

                    // create GDI path from wkb
                    var path = WkbToGdi.Parse(wkb, p => TransformTools.WgsToTile(x, y, z, p));

                    // degenerated polygon
                    if (path == null)
                        continue;

                    double popDens = reader.IsDBNull(2) ? -1 : reader.GetDouble(2);
                    paint.Color = choropleth.GetValue(popDens).WithAlpha(128);

                    surface.Canvas.DrawPath(path, paint);

                    surface.Canvas.DrawPath(path, stokePaint);
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
        }
    }


    public class Classification<V, A> where V : IComparable
    {
        public A DefaultAttribute { get; set; }

        public V MinValue { get; set; }

        public SortedList<V, A> Values { get; set; }

        public A GetValue(V key)
        {
            if (key == null)
                return DefaultAttribute;

            if (key.CompareTo(MinValue) < 0)
                return DefaultAttribute;

            // todo: maybe implement some O(log n) method
            foreach (V k in Values.Keys)
            {
                if (k.CompareTo(key) > 0)
                    return Values[k];
            }

            return DefaultAttribute;
        }
    }
}
