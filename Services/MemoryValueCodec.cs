using System.Globalization;

namespace MemoryLab.Services;

internal static class MemoryValueCodec
{
    public static int GetSize(string valueType) => valueType switch
    {
        "Byte" or "SByte" => 1,
        "Int16" or "UInt16" => 2,
        "Int32" or "UInt32" or "Float" => 4,
        "Int64" or "UInt64" or "Double" => 8,
        _ => throw new NotSupportedException($"Value type '{valueType}' belum didukung.")
    };

    public static string Decode(byte[] bytes, string valueType)
    {
        if (bytes.Length != GetSize(valueType))
            throw new InvalidOperationException("Jumlah byte yang terbaca tidak lengkap.");
        var culture = CultureInfo.InvariantCulture;
        return valueType switch
        {
            "Byte" => bytes[0].ToString(culture),
            "SByte" => unchecked((sbyte)bytes[0]).ToString(culture),
            "Int16" => BitConverter.ToInt16(bytes).ToString(culture),
            "UInt16" => BitConverter.ToUInt16(bytes).ToString(culture),
            "Int32" => BitConverter.ToInt32(bytes).ToString(culture),
            "UInt32" => BitConverter.ToUInt32(bytes).ToString(culture),
            "Int64" => BitConverter.ToInt64(bytes).ToString(culture),
            "UInt64" => BitConverter.ToUInt64(bytes).ToString(culture),
            "Float" => BitConverter.ToSingle(bytes).ToString("G9", culture),
            "Double" => BitConverter.ToDouble(bytes).ToString("G17", culture),
            _ => throw new NotSupportedException($"Value type '{valueType}' belum didukung.")
        };
    }

    public static byte[] Encode(string rawValue, string valueType)
    {
        rawValue = rawValue.Trim();
        var culture = CultureInfo.InvariantCulture;
        try
        {
            return valueType switch
            {
                "Byte" => new[] { byte.Parse(rawValue, NumberStyles.Integer, culture) },
                "SByte" => new[] { unchecked((byte)sbyte.Parse(rawValue, NumberStyles.Integer, culture)) },
                "Int16" => BitConverter.GetBytes(short.Parse(rawValue, NumberStyles.Integer, culture)),
                "UInt16" => BitConverter.GetBytes(ushort.Parse(rawValue, NumberStyles.Integer, culture)),
                "Int32" => BitConverter.GetBytes(int.Parse(rawValue, NumberStyles.Integer, culture)),
                "UInt32" => BitConverter.GetBytes(uint.Parse(rawValue, NumberStyles.Integer, culture)),
                "Int64" => BitConverter.GetBytes(long.Parse(rawValue, NumberStyles.Integer, culture)),
                "UInt64" => BitConverter.GetBytes(ulong.Parse(rawValue, NumberStyles.Integer, culture)),
                "Float" => BitConverter.GetBytes(float.Parse(rawValue, NumberStyles.Float, culture)),
                "Double" => BitConverter.GetBytes(double.Parse(rawValue, NumberStyles.Float, culture)),
                _ => throw new NotSupportedException($"Value type '{valueType}' belum didukung.")
            };
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new FormatException($"Nilai {valueType} tidak valid atau di luar rentang. Gunakan titik untuk desimal.", ex);
        }
    }
}
