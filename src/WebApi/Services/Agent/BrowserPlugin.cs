using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace WebApi.Services.Agent;

/// <summary>
/// Semantic Kernel plugin containing browser automation functions for the AI agent
/// </summary>
public class BrowserPlugin
{
    /// <summary>
    /// Clicks an element on the page using a CSS selector
    /// </summary>
    [KernelFunction]
    [Description("Clicks an element on the page. Use this when you need to interact with buttons, links, or other clickable elements. Provide the CSS selector (e.g., '#login-button', '.submit-btn', 'button[type=\"submit\"]').")]
    public string ClickElement(
        [Description("CSS selector for the element to click (e.g., '#login', 'button.submit', 'a[href=\"/login\"]')")]
        string selector,
        [Description("Brief explanation of why this element is being clicked")]
        string reasoning)
    {
        return $"Action: click, Target: {selector}, Reasoning: {reasoning}";
    }

    /// <summary>
    /// Types text into an input field
    /// </summary>
    [KernelFunction]
    [Description("Types text into an input field, textarea, or other text input element. Use this for entering usernames, passwords, search terms, or any text input. Provide the CSS selector and the text to type.")]
    public string TypeText(
        [Description("CSS selector for the input field (e.g., '#email', 'input[name=\"username\"]', '#password')")]
        string selector,
        [Description("The text to type into the field")]
        string text,
        [Description("Brief explanation of why this text is being entered")]
        string reasoning)
    {
        return $"Action: type, Target: {selector}, Value: {text}, Reasoning: {reasoning}";
    }

    /// <summary>
    /// Navigates to a different URL
    /// </summary>
    [KernelFunction]
    [Description("Navigates the browser to a different URL. Use this when you need to go to a different page or website. Provide the full URL or relative path.")]
    public string Navigate(
        [Description("The URL to navigate to (e.g., 'https://example.com/login', '/dashboard', 'https://payments.example.com')")]
        string url,
        [Description("Brief explanation of why navigation is needed")]
        string reasoning)
    {
        return $"Action: navigate, Target: {url}, Reasoning: {reasoning}";
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
        return $"Action: wait, Duration: {seconds}, Reasoning: {reasoning}";
    }

    /// <summary>
    /// Scrolls the page in a direction
    /// </summary>
    [KernelFunction]
    [Description("Scrolls the page up or down. Use this when you need to see content that is not currently visible on the page. Direction should be 'up' or 'down'.")]
    public string Scroll(
        [Description("Direction to scroll: 'up' or 'down'")]
        string direction,
        [Description("Brief explanation of why scrolling is needed")]
        string reasoning)
    {
        return $"Action: scroll, Target: {direction}, Reasoning: {reasoning}";
    }

    /// <summary>
    /// Marks the task as complete
    /// </summary>
    [KernelFunction]
    [Description("Marks the current task as complete. Use this when the user's goal has been successfully achieved. Provide a brief message summarizing what was accomplished.")]
    public string Complete(
        [Description("Brief message summarizing what was accomplished")]
        string message)
    {
        return $"Action: complete, Reasoning: {message}";
    }
}
