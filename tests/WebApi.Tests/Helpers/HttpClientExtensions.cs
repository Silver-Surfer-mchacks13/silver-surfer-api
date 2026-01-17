using System.Net.Http.Json;
using System.Text.Json;

namespace WebApi.Tests.Helpers;

public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static Task<HttpResponseMessage> PostAsJsonSnakeCaseAsync<T>(this HttpClient client, string requestUri, T value)
    {
        var json = JsonSerializer.Serialize(value, SnakeCaseOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return client.PostAsync(requestUri, content);
    }

    public static Task<HttpResponseMessage> PutAsJsonSnakeCaseAsync<T>(this HttpClient client, string requestUri, T value)
    {
        var json = JsonSerializer.Serialize(value, SnakeCaseOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return client.PutAsync(requestUri, content);
    }

    public static Task<HttpResponseMessage> PatchAsJsonSnakeCaseAsync<T>(this HttpClient client, string requestUri, T value)
    {
        var json = JsonSerializer.Serialize(value, SnakeCaseOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        return client.PatchAsync(requestUri, content);
    }

    public static Task<T?> ReadFromJsonSnakeCaseAsync<T>(this HttpContent content)
    {
        return content.ReadFromJsonAsync<T>(SnakeCaseOptions);
    }
}
