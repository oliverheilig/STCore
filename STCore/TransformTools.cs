using System;
using SkiaSharp;

namespace STCore
{
    /// <summary>
    /// A tools class that does the arithmetics for tiled maps
    /// </summary>
    public static class TransformTools
    {
        /// <summary>
        /// Convert a WGS84 coordinate (Lon/Lat) to generic spherical mercator.
        /// When using tiles with wgs, the actual earth radius doesn't matter, we can just use radius 1.
        /// To use this formula with "Google Mercator", you have to multiply the output coordinates by 6378137.
        /// For "PTV Mercator" use 6371000
        /// </summary>
        public static SKPoint WgsToSphereMercator(SKPoint point)
        {
            double x = point.X * Math.PI / 180.0;
            double y = Math.Log(Math.Tan(Math.PI / 4.0 + point.Y * Math.PI / 360.0));

            return new SKPoint((float)x, (float)y);
        }

        /// <summary>
        /// The reverse of the function above
        /// To use this formula with "Google Mercator", you have to divide the input coordinates by 6378137.
        /// For "PTV Mercator" use 6371000
        /// </summary>
        public static SKPoint SphereMercatorToWgs(SKPoint point)
        {
            double x = (180 / Math.PI) * point.X;
            double y = (360 / Math.PI) * (Math.Atan(Math.Exp(point.Y)) - (Math.PI / 4));

            return new SKPoint((float)x, (float)y);
        }

        /// <summary>
        /// Calculate the Mercator bounds for a tile key
        /// </summary>
        public static SKRect TileToSphereMercator(uint x, uint y, uint z)
        {
            // the width of a tile (when the earth has radius 1)
            double arc = Math.PI * 2.0 / Math.Pow(2, z);

            double x1 = -Math.PI + x * arc;
            double x2 = x1 + arc;

            double y1 = Math.PI - y * arc;
            double y2 = y1 - arc;

            return new SKRect((float)x1, (float)y2, (float)x2, (float)y1);
        }

        /// <summary>
        /// Calculate WGS (Lon/Lat) bounds for a tile key
        /// </summary>
        public static SKRect TileToWgs(uint x, uint y, uint z, int bleedingPixels = 0)
        {
            var rect = TileToSphereMercator(x, y, z);

            if(bleedingPixels != 0)
            { 
                float bleedingFactor = bleedingPixels / 256.0f * 2;

                rect.Inflate(rect.Width * bleedingFactor, rect.Height * bleedingFactor);
            }

            var p0 = SphereMercatorToWgs(new SKPoint(rect.Left, rect.Top));
            var p1 = SphereMercatorToWgs(new SKPoint(rect.Right, rect.Bottom));
            return new SKRect(p0.X, p0.Y, p1.X, p1.Y);
        }

        /// <summary>
        /// Convert a point relative to a mercator viewport to a point relative to an image
        /// </summary>
        public static SKPoint MercatorToImage(SKRect mercatorRect, SKSize imageSize, SKPoint mercatorPoint)
        {
            return new SKPoint(
              (int)((mercatorPoint.X - mercatorRect.Left) / (mercatorRect.Right - mercatorRect.Left) * imageSize.Width),
              (int)(imageSize.Height - (mercatorPoint.Y - mercatorRect.Top) / (mercatorRect.Bottom - mercatorRect.Top) * imageSize.Height));
        }

        /// <summary>
        /// Convert a WGS (Lon,Lat) coordinate to a point relative to a tile image
        /// </summary>
        public static SKPoint WgsToTile(uint x, uint y, uint z, SKPoint wgsPoint, float clipWgsAtDegrees = 85.05f)
        {
            if (clipWgsAtDegrees < 90f)
                wgsPoint = ClipWgsPoint(wgsPoint, clipWgsAtDegrees);

            return MercatorToImage(TileToSphereMercator(x, y, z), new SKSize(256, 256), WgsToSphereMercator(wgsPoint));
        }

        /// <summary>
        /// Clip the latitude value to avoid overflow at the poles
        /// </summary>
        public static SKPoint ClipWgsPoint(SKPoint p, float degrees = 85.05f)
        {
            if (p.Y > degrees)
                p.Y = degrees;
            if (p.Y < -degrees)
                p.Y = -degrees;

            return p;
        }
    }
}
