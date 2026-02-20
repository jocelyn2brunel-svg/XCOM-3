import json
import base64
import math

def is_inside(poly, x, y):
    inside = False
    n = len(poly)
    if n == 0: return False
    p1x, p1y = poly[0]
    for i in range(1, n + 1):
        p2x, p2y = poly[i % n]
        if y > min(p1y, p2y):
            if y <= max(p1y, p2y):
                if x <= max(p1x, p2x):
                    if p1y != p2y:
                        xints = (y - p1y) * (p2x - p1x) / (p2y - p1y) + p1x
                    if p1x == p2x or x <= xints:
                        inside = not inside
        p1x, p1y = p2x, p2y
    return inside

def process():
    import subprocess
    subprocess.run(["curl", "-s", "https://raw.githubusercontent.com/johan/world.geo.json/master/countries.geo.json", "-o", "countries.json"])

    with open('countries.json', 'r') as f:
        data = json.load(f)

    mask = [0] * (360 * 180)
    countries_polygons = []

    print("Processing countries...")
    for feature in data['features']:
        geometry = feature['geometry']
        polys = []
        if geometry['type'] == 'Polygon':
            polys.append(geometry['coordinates'])
        elif geometry['type'] == 'MultiPolygon':
            polys.extend(geometry['coordinates'])

        for poly in polys:
            ring = poly[0]
            simplified_ring = []
            for i, pt in enumerate(ring):
                if i == 0 or i == len(ring)-1:
                    simplified_ring.append(pt)
                else:
                    prev = simplified_ring[-1]
                    dist = abs(pt[0]-prev[0]) + abs(pt[1]-prev[1])
                    if dist > 0.5:
                        simplified_ring.append(pt)
            countries_polygons.append(simplified_ring)

            lons = [p[0] for p in ring]
            lats = [p[1] for p in ring]
            min_lon, max_lon = int(math.floor(min(lons))), int(math.ceil(max(lons)))
            min_lat, max_lat = int(math.floor(min(lats))), int(math.ceil(max(lats)))

            for lat_i in range(min_lat, max_lat + 1):
                for lon_i in range(min_lon, max_lon + 1):
                    if is_inside(ring, lon_i, lat_i):
                        m_lon = (lon_i + 180) % 360
                        m_lat = max(0, min(179, lat_i + 90))
                        idx = m_lat * 360 + m_lon
                        mask[idx] = 1

    mask_bytes = bytearray()
    for i in range(0, len(mask), 8):
        byte = 0
        for j in range(8):
            if i + j < len(mask) and mask[i+j]:
                byte |= (1 << j)
        mask_bytes.append(byte)
    mask_base64 = base64.b64encode(mask_bytes).decode('ascii')

    with open('Scripts/WorldData.cs', 'w') as f:
        f.write("using System;\n\nnamespace XCOM_3\n{\n")
        f.write("    public static class WorldData\n    {\n")
        f.write(f"        public static readonly string LandMaskBase64 = \"{mask_base64}\";\n")
        f.write("        private static byte[] _landMask;\n\n")
        f.write("        public static bool IsLand(float lat, float lon)\n        {\n")
        f.write("            if (_landMask == null) _landMask = Convert.FromBase64String(LandMaskBase64);\n")
        f.write("            int x = (int)Math.Floor(lon + 180f);\n")
        f.write("            int y = (int)Math.Floor(lat + 90f);\n")
        f.write("            x = (x % 360 + 360) % 360;\n")
        f.write("            if (y < 0) y = 0; else if (y > 179) y = 179;\n")
        f.write("            int idx = y * 360 + x;\n")
        f.write("            return (_landMask[idx / 8] & (1 << (idx % 8))) != 0;\n")
        f.write("        }\n\n")
        f.write("        public static readonly float[][] CountryBorders = new float[][] {\n")
        for poly in countries_polygons:
            if len(poly) < 2: continue
            coords = [f"{round(pt[1], 2)}f, {round(pt[0], 2)}f" for pt in poly]
            f.write("            new float[] { " + ", ".join(coords) + " },\n")
        f.write("        };\n")
        f.write("    }\n}\n")

if __name__ == "__main__":
    process()
