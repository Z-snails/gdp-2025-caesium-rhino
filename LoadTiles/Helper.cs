using System;
using Rhino.Geometry;
using TilesData;

namespace LoadTiles;

public static class Helper {

    // WGS84 ellipsoid parameters
    private const double EQ_RADIUS = 6378137.0; // Equatorial radius in meters
    private const double FLATTENING = 1 / 298.257223563; // Flattening
    private const double SQ_FST_ECCENTRICITY = (2 - FLATTENING) * FLATTENING; // First eccentricity squared
    
    /// <summary>
    /// Converts latitude and longitude in degrees to ECEF coordinates (EPSG:4978).
    /// </summary>
    /// <param name="latitude">Latitude (in degrees)</param>
    /// <param name="longitude">Longitude (in degrees)</param>
    /// <param name="altitude">Altitude (in metres), default 0</param>
    /// <returns>A Point3d object at the corresponding ECEF coordinates.</returns>
    public static Point3d LatLonToEPSG4978(double latitude, double longitude, double altitude = 0)
    {
        // Convert degrees to radians
        double latRad = latitude * Math.PI / 180.0;
        double lonRad = longitude * Math.PI / 180.0;

        // Compute the prime vertical radius of curvature
        double N = EQ_RADIUS / Math.Sqrt(1 - SQ_FST_ECCENTRICITY * Math.Pow(Math.Sin(latRad), 2));

        // Compute ECEF coordinates
        double X = (N + altitude) * Math.Cos(latRad) * Math.Cos(lonRad);
        double Y = (N + altitude) * Math.Cos(latRad) * Math.Sin(lonRad);
        double Z = ((1 - SQ_FST_ECCENTRICITY) * N + altitude) * Math.Sin(latRad);

        return new Point3d(X, Y, Z);
    }

    /// <summary>
    /// Converts ECEF coordinates (EPSG:4978) to latitude and longitude in radians.
    /// </summary>
    /// <param name="point">Point3d object storing ECEF coordinates</param>
    /// <returns>(lat, lon, altitude) with lat/lon in *radians*.</returns>
    static (double latitude, double longitude, double altitude) EPSG4978ToLatLonRadians(Point3d point)
    {
        const double EPSILON = 1e-12;
        double X = point.X;
        double Y = point.Y;
        double Z = point.Z;

        double lonRad = Math.Atan2(Y, X);
        double p = Math.Sqrt(X * X + Y * Y);
        double latRad = Math.Atan2(Z, p * (1 - SQ_FST_ECCENTRICITY));

        // Iteratively finds the latitude using Newton's method
        double N, h, latPrev;
        do {
            latPrev = latRad;
            N = EQ_RADIUS / Math.Sqrt(1 - SQ_FST_ECCENTRICITY * Math.Pow(Math.Sin(latRad), 2));
            h = p / Math.Cos(latRad) - N;
            latRad = Math.Atan2(Z, p * (1 - SQ_FST_ECCENTRICITY * N / (N + h)));
        } while (Math.Abs(latRad - latPrev) > EPSILON);

        return (latRad, lonRad, h);
    }

    /// <summary>
    /// Calculates the distance between two points on the Earth's surface using the Haversine formula.
    /// The points are specified in radians.
    /// </summary>
    private static double GroundDistance(double lat1, double lon1, double lat2, double lon2) {
        // Haversine formula to calculate the distance between two points on the Earth
        double dLat = lat2 - lat1;
        double dLon = lon2 - lon1;
        double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLon / 2), 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        double distance = EQ_RADIUS * c;  // Distance in meters
        return distance;
    }

    /// <summary>
    /// Calculates the distance from a point to the centre of a bounding volume.
    /// </summary>
    public static double PointDistanceToBoundingVolume(Point3d point, BoundingVolume boundingVolume) {
        if (boundingVolume.Box != null) {
            (double lat1, double lon1, _) = EPSG4978ToLatLonRadians(boundingVolume.Box.Center);
            (double lat2, double lon2, _) = EPSG4978ToLatLonRadians(point);
            return GroundDistance(lat1, lon1, lat2, lon2);
        } else if (boundingVolume.Sphere != null) {
            (double lat1, double lon1, _) = EPSG4978ToLatLonRadians(boundingVolume.Sphere.Center);
            (double lat2, double lon2, _) = EPSG4978ToLatLonRadians(point);
            return GroundDistance(lat1, lon1, lat2, lon2);
        } else if (boundingVolume.Region != null) {
            double lat1 = (boundingVolume.Region.South + boundingVolume.Region.North) / 2.0;
            double lon1 = (boundingVolume.Region.West + boundingVolume.Region.East) / 2.0;
            (double lat2, double lon2, _) = EPSG4978ToLatLonRadians(point);
            return GroundDistance(lat1, lon1, lat2, lon2);
        } else {
            throw new NotImplementedException("Bounding volume type not supported.");
        }
    }

    private static double AngularDistance(double angle1, double angle2) {
        double diff = angle1 - angle2;
        while (diff > Math.PI) {
            diff -= 2 * Math.PI;
        }
        while (diff < -Math.PI) {
            diff += 2 * Math.PI;
        }
        return diff;
    }

    /// <summary>
    /// Calculates the distance from a point to the nearest edge of a tile.
    /// If the point is inside the tile, the distance is 0.
    /// Only implemented for tiles with a rectangular or region bounding volume.
    /// </summary>
    public static double PointDistanceToTile(Point3d point, Tile tile) {
        if (tile.BoundingVolume.Box != null) 
        {
            TileBoundingBox box = tile.BoundingVolume.Box;
            if (TileBoundingBox.IsInBox(box, point)) {
                return 0.0;
            } else {
                Brep brepBox = box.AsBox().ToBrep();
                Point3d boundaryPoint = brepBox.ClosestPoint(point);
                (double lat1, double lon1, _) = EPSG4978ToLatLonRadians(point);
                (double lat2, double lon2, _) = EPSG4978ToLatLonRadians(boundaryPoint);
                return GroundDistance(lat1, lon1, lat2, lon2);
            }
        }
        else if (tile.BoundingVolume.Region != null)
        {
            BoundingRegion region = tile.BoundingVolume.Region;

            (double lat1, double lon1, _) = EPSG4978ToLatLonRadians(point);
            // (lat2, lon2) is the closest point on the region to the point
            double lat2 = 
                (lat1 > region.North) ? region.North :
                (lat1 < region.South) ? region.South : 
                lat1;
            double eastDiff = AngularDistance(region.East, lon1);
            double westDiff = AngularDistance(region.West, lon1);
            double lon2 =
                (lon1 >= region.West && lon1 <= region.East) ? lon1 :
                (region.West > region.East && (lon1 <= region.East || lon1 >= region.West)) ? lon1 :
                (eastDiff < westDiff) ? region.East :
                region.West;
            if (lat1 == lat2 && lon1 == lon2) return 0.0; // Point is inside the region
            else return GroundDistance(lat1, lon1, lat2, lon2);
        }
        else
        {
            throw new NotImplementedException("Sphere bounding volumes are not supported yet.");
        }

    }
}