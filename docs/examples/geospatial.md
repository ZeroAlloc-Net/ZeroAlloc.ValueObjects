# Geospatial Domain Examples

## `Coordinates` — latitude/longitude pair (struct)

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public readonly partial struct Coordinates
{
    public double Latitude { get; }
    public double Longitude { get; }

    public Coordinates(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Must be between -90 and 90");
        if (longitude < -180 || longitude > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Must be between -180 and 180");
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Distance in metres using the Haversine formula.</summary>
    public double DistanceTo(Coordinates other)
    {
        const double R = 6_371_000;
        var φ1 = Latitude  * Math.PI / 180;
        var φ2 = other.Latitude  * Math.PI / 180;
        var Δφ = (other.Latitude  - Latitude)  * Math.PI / 180;
        var Δλ = (other.Longitude - Longitude) * Math.PI / 180;
        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                Math.Cos(φ1) * Math.Cos(φ2) *
                Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public override string ToString() => $"({Latitude:F6}, {Longitude:F6})";
}
```

```csharp
var amsterdam = new Coordinates(52.3676, 4.9041);
var london    = new Coordinates(51.5074, -0.1278);
var amsterdam2 = new Coordinates(52.3676, 4.9041);

amsterdam == london    // false
amsterdam == amsterdam2  // true

double distanceMetres = amsterdam.DistanceTo(london);  // ~357,000 m

// Cache geofence lookups keyed by exact coordinate
var regionCache = new Dictionary<Coordinates, string>
{
    [amsterdam] = "Netherlands",
    [london]    = "United Kingdom",
};
```

---

## `GeoRegion` — bounding box

```csharp
[ValueObject]
public partial class GeoRegion
{
    public Coordinates SouthWest { get; }
    public Coordinates NorthEast { get; }
    public string Name { get; }

    public GeoRegion(Coordinates sw, Coordinates ne, string name)
    {
        SouthWest = sw; NorthEast = ne; Name = name;
    }

    public bool Contains(Coordinates point) =>
        point.Latitude  >= SouthWest.Latitude  && point.Latitude  <= NorthEast.Latitude &&
        point.Longitude >= SouthWest.Longitude && point.Longitude <= NorthEast.Longitude;
}
```

```csharp
var benelux = new GeoRegion(
    new Coordinates(49.5, 2.5),
    new Coordinates(53.5, 7.2),
    "Benelux");

var amsterdam = new Coordinates(52.3676, 4.9041);
benelux.Contains(amsterdam)  // true

// Two regions are equal if their coordinates match (Name is included by default)
var sameRegion = new GeoRegion(
    new Coordinates(49.5, 2.5),
    new Coordinates(53.5, 7.2),
    "Benelux");

benelux == sameRegion  // true
```
