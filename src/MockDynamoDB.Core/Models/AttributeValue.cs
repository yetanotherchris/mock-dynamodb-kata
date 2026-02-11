using System.Text.Json;
using System.Text.Json.Serialization;

namespace MockDynamoDB.Core.Models;

[JsonConverter(typeof(AttributeValueConverter))]
public class AttributeValue
{
    public string? S { get; set; }
    public string? N { get; set; }
    public string? B { get; set; }
    public bool? BOOL { get; set; }
    public bool? NULL { get; set; }
    public List<string>? SS { get; set; }
    public List<string>? NS { get; set; }
    public List<string>? BS { get; set; }
    public List<AttributeValue>? L { get; set; }
    public Dictionary<string, AttributeValue>? M { get; set; }

    public AttributeValueType Type => this switch
    {
        { S: not null } => AttributeValueType.S,
        { N: not null } => AttributeValueType.N,
        { B: not null } => AttributeValueType.B,
        { BOOL: not null } => AttributeValueType.BOOL,
        { NULL: not null } => AttributeValueType.NULL,
        { SS: not null } => AttributeValueType.SS,
        { NS: not null } => AttributeValueType.NS,
        { BS: not null } => AttributeValueType.BS,
        { L: not null } => AttributeValueType.L,
        { M: not null } => AttributeValueType.M,
        _ => AttributeValueType.Unknown
    };

    public AttributeValue DeepClone()
    {
        var clone = new AttributeValue
        {
            S = S,
            N = N,
            B = B,
            BOOL = BOOL,
            NULL = NULL,
            SS = SS != null ? new List<string>(SS) : null,
            NS = NS != null ? new List<string>(NS) : null,
            BS = BS != null ? new List<string>(BS) : null,
            L = L?.Select(v => v.DeepClone()).ToList(),
            M = M?.ToDictionary(kv => kv.Key, kv => kv.Value.DeepClone())
        };
        return clone;
    }
}

public enum AttributeValueType
{
    Unknown,
    S,
    N,
    B,
    BOOL,
    NULL,
    SS,
    NS,
    BS,
    L,
    M
}

public class AttributeValueConverter : JsonConverter<AttributeValue>
{
    public override AttributeValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("AttributeValue must be a JSON object");

        var av = new AttributeValue();
        reader.Read();

        while (reader.TokenType != JsonTokenType.EndObject)
        {
            var prop = reader.GetString()!;
            reader.Read();

            switch (prop)
            {
                case "S":
                    av.S = reader.GetString();
                    break;
                case "N":
                    av.N = reader.GetString();
                    break;
                case "B":
                    av.B = reader.GetString();
                    break;
                case "BOOL":
                    av.BOOL = reader.GetBoolean();
                    break;
                case "NULL":
                    av.NULL = reader.GetBoolean();
                    break;
                case "SS":
                    av.SS = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                    break;
                case "NS":
                    av.NS = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                    break;
                case "BS":
                    av.BS = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                    break;
                case "L":
                    av.L = JsonSerializer.Deserialize<List<AttributeValue>>(ref reader, options);
                    break;
                case "M":
                    av.M = JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
            reader.Read();
        }

        return av;
    }

    public override void Write(Utf8JsonWriter writer, AttributeValue value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        if (value.S != null)
        {
            writer.WriteString("S", value.S);
        }
        else if (value.N != null)
        {
            writer.WriteString("N", value.N);
        }
        else if (value.B != null)
        {
            writer.WriteString("B", value.B);
        }
        else if (value.BOOL != null)
        {
            writer.WriteBoolean("BOOL", value.BOOL.Value);
        }
        else if (value.NULL != null)
        {
            writer.WriteBoolean("NULL", value.NULL.Value);
        }
        else if (value.SS != null)
        {
            writer.WritePropertyName("SS");
            JsonSerializer.Serialize(writer, value.SS, options);
        }
        else if (value.NS != null)
        {
            writer.WritePropertyName("NS");
            JsonSerializer.Serialize(writer, value.NS, options);
        }
        else if (value.BS != null)
        {
            writer.WritePropertyName("BS");
            JsonSerializer.Serialize(writer, value.BS, options);
        }
        else if (value.L != null)
        {
            writer.WritePropertyName("L");
            JsonSerializer.Serialize(writer, value.L, options);
        }
        else if (value.M != null)
        {
            writer.WritePropertyName("M");
            JsonSerializer.Serialize(writer, value.M, options);
        }

        writer.WriteEndObject();
    }
}
