using System.Text.Json;
using System.Text.Json.Serialization;

namespace WingetIntune.Tests;

internal static class HttpContentExtensions
{
    public static bool IsJson(this HttpContent? content)
    {
        return content?.Headers.ContentType?.MediaType == "application/json";
    }

    public static bool ValidateJsonBody(this HttpContent? content, Dictionary<string, object> bodyValues)
    {
        if (content is null || !content.IsJson())
        {
            return false;
        }

        var json = content.ReadAsStringAsync().Result;
        try
        {
            var body = JsonSerializer.Deserialize<JsonContentBody>(json);

            if (body is null || body.Data is null)
            {
                return false;
            }
            foreach (var kvp in bodyValues)
            {
                if (!body.Data.ContainsKey(kvp.Key) || !body.Data[kvp.Key].ValueEquals(kvp.Value.ToString()))
                {
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize JSON: {json}", ex);
        }
    }

    public class JsonContentBody
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? Data { get; set; }
    }
}
