using System.Diagnostics.CodeAnalysis;

namespace WebApi.Configuration.Options;

/// <summary>
/// Gemini/Vertex AI service settings
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class GeminiOptions
{
    public const string SectionName = "Gemini";
    
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = "us-central1";
    public string Model { get; set; } = "gemini-2.0-flash-exp";
    public int MaxTokens { get; set; } = 8192;
    public float Temperature { get; set; } = 0.7f;
}
