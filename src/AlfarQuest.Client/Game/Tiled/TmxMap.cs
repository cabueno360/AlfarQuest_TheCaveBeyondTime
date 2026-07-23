using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace AlfarQuest.Client.Game.Tiled;

/// <summary>A Tiled .tmx map, read as data.
///
/// Deliberately small: it knows how to read a map file and nothing about the game.
/// Layer names, object names and custom properties carry all the meaning, and the
/// map builder (Overworld.Tmx) is what turns them into a world — so adding a new
/// kind of object to a map never touches this file.
///
/// Only what the project's maps actually use is supported: orthogonal maps, finite
/// size, external tilesets, and layer data in Tiled's default base64+zlib (CSV is
/// accepted too, for a hand-edited map).</summary>
public sealed class TmxMap
{
    public int Width, Height, TileWidth, TileHeight;
    public Dictionary<string, string> Properties = [];

    /// <summary>A map-level custom property — what the map says about itself:
    /// its display name, its stage, the regions it adjoins.</summary>
    public string Property(string key, string fallback = "") =>
        Properties.TryGetValue(key, out var v) && v.Length > 0 ? v : fallback;

    /// <summary>Layer name → global tile ids, row-major, 0 for an empty cell.</summary>
    public Dictionary<string, int[]> TileLayers = [];

    /// <summary>Object layer name → its objects, in file order.</summary>
    public Dictionary<string, List<TmxObject>> ObjectLayers = [];

    public int[]? Layer(string name) => TileLayers.GetValueOrDefault(name);

    public IReadOnlyList<TmxObject> Objects(string layer) =>
        ObjectLayers.TryGetValue(layer, out var l) ? l : [];

    /// <summary>Whether a cell of a layer is painted. Layers are how terrain type is
    /// decided — see the precedence in the map builder.</summary>
    public bool Painted(int[]? layer, int x, int y) =>
        layer is not null && x >= 0 && y >= 0 && x < Width && y < Height && layer[y * Width + x] != 0;

    public static TmxMap Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidDataException("tmx: no root element");
        var map = new TmxMap
        {
            Width = Int(root, "width"), Height = Int(root, "height"),
            TileWidth = Int(root, "tilewidth"), TileHeight = Int(root, "tileheight"),
        };
        map.Properties = ReadProperties(root.Element("properties"));

        foreach (var layer in root.Elements("layer"))
        {
            var name = (string?)layer.Attribute("name") ?? "";
            var data = layer.Element("data");
            if (data is null) continue;
            map.TileLayers[name] = DecodeLayer(data, Int(layer, "width"), Int(layer, "height"));
        }

        foreach (var group in root.Elements("objectgroup"))
        {
            var name = (string?)group.Attribute("name") ?? "";
            var list = new List<TmxObject>();
            foreach (var o in group.Elements("object"))
                list.Add(new TmxObject
                {
                    Name = (string?)o.Attribute("name") ?? "",
                    X = Flt(o, "x"), Y = Flt(o, "y"),
                    Width = Flt(o, "width"), Height = Flt(o, "height"),
                    Properties = ReadProperties(o.Element("properties")),
                });
            map.ObjectLayers[name] = list;
        }
        return map;
    }

    // ---- reading -----------------------------------------------------

    static int Int(XElement e, string a) =>
        int.TryParse((string?)e.Attribute(a), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    static float Flt(XElement e, string a) =>
        float.TryParse((string?)e.Attribute(a), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    static Dictionary<string, string> ReadProperties(XElement? props)
    {
        var d = new Dictionary<string, string>();
        if (props is null) return d;
        foreach (var p in props.Elements("property"))
        {
            var n = (string?)p.Attribute("name");
            if (n is null) continue;
            // Tiled writes a value either as an attribute or, for multi-line text,
            // as the element's own content.
            d[n] = (string?)p.Attribute("value") ?? p.Value;
        }
        return d;
    }

    /// <summary>A layer's tile ids. base64+zlib is what Tiled writes by default and
    /// what the generator produces; CSV is accepted so a map can be hand-edited.
    /// The high flip bits are masked off — this project does not flip terrain.</summary>
    static int[] DecodeLayer(XElement data, int w, int h)
    {
        var encoding = (string?)data.Attribute("encoding");
        var count = Math.Max(0, w * h);
        var ids = new int[count];

        if (encoding == "csv")
        {
            var i = 0;
            foreach (var part in data.Value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (i >= count) break;
                _ = uint.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v);
                ids[i++] = (int)(v & 0x1FFFFFFF);
            }
            return ids;
        }

        if (encoding != "base64")
            throw new InvalidDataException($"tmx: unsupported layer encoding '{encoding}'");

        var raw = Convert.FromBase64String(data.Value.Trim());
        var compression = (string?)data.Attribute("compression");
        raw = compression switch
        {
            null or "" => raw,
            "zlib" => Inflate(raw, zlibHeader: true),
            "gzip" => Inflate(raw, zlibHeader: false, gzip: true),
            _ => throw new InvalidDataException($"tmx: unsupported layer compression '{compression}'"),
        };

        for (int i = 0; i < count && (i + 1) * 4 <= raw.Length; i++)
            ids[i] = (int)(BitConverter.ToUInt32(raw, i * 4) & 0x1FFFFFFF);
        return ids;
    }

    static byte[] Inflate(byte[] raw, bool zlibHeader, bool gzip = false)
    {
        using var src = new MemoryStream(raw);
        using Stream dec = gzip
            ? new GZipStream(src, CompressionMode.Decompress)
            : zlibHeader ? new ZLibStream(src, CompressionMode.Decompress)
                         : new DeflateStream(src, CompressionMode.Decompress);
        using var outp = new MemoryStream();
        dec.CopyTo(outp);
        return outp.ToArray();
    }
}

/// <summary>One object from an object layer. Position is in MAP pixels; what it
/// means is carried by its layer, its name and its custom properties.</summary>
public sealed class TmxObject
{
    public string Name = "";
    public float X, Y, Width, Height;
    public Dictionary<string, string> Properties = [];

    public string Str(string key, string fallback = "") =>
        Properties.TryGetValue(key, out var v) && v.Length > 0 ? v : fallback;

    public float Num(string key, float fallback = 0f) =>
        Properties.TryGetValue(key, out var v)
        && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;

    public int Int(string key, int fallback = 0) =>
        Properties.TryGetValue(key, out var v)
        && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : fallback;

    public bool Flag(string key, bool fallback = false) =>
        Properties.TryGetValue(key, out var v) ? v is "true" or "1" : fallback;

    /// <summary>Whether the map said anything at all about this key. Lets a reader
    /// tell "the map set it false" from "the map is silent", which a bool cannot.</summary>
    public bool Has(string key) => Properties.ContainsKey(key);
}
