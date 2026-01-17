using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace WebApi.Services.Agent;

/// <summary>
/// Semantic Kernel plugin containing browser automation functions for the AI agent (MVP: click, wait, complete)
/// </summary>
public class BrowserPlugin
{
    /// <summary>
    /// Clicks an element on the page using an XPath expression
    /// </summary>
    [KernelFunction]
    [Description("Clicks an element on the page. Use this when you need to interact with buttons, links, or other clickable elements. Provide the XPath expression (e.g., '//button[@id=\"login-button\"]', '//div[@class=\"submit-btn\"]', '//button[@type=\"submit\"]').")]
    public string ClickElement(
        [Description("XPath expression for the element to click (e.g., '//button[@id=\"login\"]', '//a[@href=\"/login\"]', '//input[@type=\"submit\"]')")]
        string xpath,
        [Description("Brief explanation of why this element is being clicked")]
        string reasoning)
    {
        // Function executed - return value is used by Semantic Kernel for function calling flow
        // The actual action extraction happens from the function call metadata
        return $"Clicked element: {xpath}";
    }

    /// <summary>
    /// Waits for a specified number of seconds
    /// </summary>
    [KernelFunction]
    [Description("Waits for a specified number of seconds. Use this when you need to wait for page content to load, animations to complete, or for time-based delays. Keep waits short (1-5 seconds) unless necessary.")]
    public string Wait(
        [Description("Number of seconds to wait (typically 1-5 seconds)")]
        int seconds,
        [Description("Brief explanation of why waiting is necessary")]
        string reasoning)
    {
        // Function executed - return value is used by Semantic Kernel for function calling flow
        return $"Waiting {seconds} seconds";
    }

    /// <summary>
    /// Displays a message to the user (non-terminal - frontend should continue sending requests)
    /// </summary>
    [KernelFunction]
    [Description("Displays a message to the user. Use this when you need to communicate information, ask for clarification, or provide status updates. The frontend will display this message but continue processing. This is different from Complete which stops the conversation.")]
    public string Message(
        [Description("Message to display to the user")]
        string message)
    {
        // Function executed - return value is used by Semantic Kernel for function calling flow
        return $"Message: {message}";
    }

    /// <summary>
    /// Marks the task as complete (terminal - frontend should stop sending requests)
    /// </summary>
    [KernelFunction]
    [Description("Marks the current task as complete. Use this when the user's goal has been successfully achieved. This stops the conversation - the frontend will not send any more requests. Provide a brief message summarizing what was accomplished.")]
    public string Complete(
        [Description("Brief message summarizing what was accomplished")]
        string message)
    {
        // Function executed - return value is used by Semantic Kernel for function calling flow
        return $"Task completed: {message}";
    }
}
