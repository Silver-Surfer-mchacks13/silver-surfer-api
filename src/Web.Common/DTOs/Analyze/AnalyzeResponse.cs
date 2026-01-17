namespace Web.Common.DTOs.Analyze;

public class AnalyzeResponse
{
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public int? TokensUsed { get; set; }
}
