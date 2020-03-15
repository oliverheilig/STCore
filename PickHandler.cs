using System;
using System.Data.SQLite;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading.Tasks;
using System.Globalization;

namespace STCore
{
    public class PickHandler
    {
        public static async Task PickElement(HttpContext context, SQLiteConnection cn)
        {
            double lat = double.Parse(context.GetRouteValue("lat").ToString(), CultureInfo.InvariantCulture);
            double lng = double.Parse(context.GetRouteValue("lng").ToString(), CultureInfo.InvariantCulture);

            // Select elements containing the point, pre-filter with mbr-cache to optimize performance
            var query = FormattableString.Invariant(
                $@"
                    SELECT WorldData.Id, AsGeoJSON(Geometry), Name, Region, Area, Pop from 
                        (SELECT * from WorldGeom 
                            WHERE ROWID IN 
                                (Select rowid FROM cache_WorldGeom_Geometry WHERE
                                    mbr = FilterMbrIntersects({lng}, {lat}, {lng}, {lat}))
                            AND Intersects(Geometry, MakePoint({lng}, {lat}))) as g                 
                        JOIN WorldData on WorldData.Id = g.Id 
                    ");

            using (var command = new SQLiteCommand(query, cn))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string str = reader.GetString(1);
                    string name = reader.GetString(2);
                    string region = reader.GetString(3);
                    double area = reader.GetDouble(4);
                    double pop = reader.GetDouble(5);

                    // build response
                    context.Response.ContentType = "text/json";
                    await context.Response.WriteAsync(string.Format(CultureInfo.InvariantCulture,
                        @"{{""geometry"": {0},""type"": ""Feature""," +
                        @"""properties"": {{""name"": ""{1}"", ""region"": ""{2}"", ""area"": ""{3}"", ""pop"": ""{4}""}}}}",
                        str, name, region, area, pop));

                    return;
                }
            }

            // no result - return empty json
            context.Response.ContentType = "text/json";
            await context.Response.WriteAsync("{}");
        }
    }
}
