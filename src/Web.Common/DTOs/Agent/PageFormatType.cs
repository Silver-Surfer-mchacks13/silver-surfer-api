namespace Web.Common.DTOs.Agent;

/// <summary>
/// Represents the format type of page data
/// </summary>
public enum PageFormatType
{
    /// <summary>
    /// HTML format - raw HTML content
    /// </summary>
    Html,
    
    /// <summary>
    /// Structured JSON format - parsed page data with elements array
    /// </summary>
    StructuredJson
}
