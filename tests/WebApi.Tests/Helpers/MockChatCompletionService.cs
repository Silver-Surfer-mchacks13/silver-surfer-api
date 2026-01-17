using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApi.Tests.Helpers;

/// <summary>
/// Mock implementation of IChatCompletionService for testing
/// Returns predictable responses that simulate AI agent behavior
/// </summary>
public class MockChatCompletionService : IChatCompletionService
{
    private int _callCount = 0;

    public string? ModelId => "mock-gemini-model";
    
    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
    {
        { "ModelId", ModelId },
        { "ServiceId", "MockChatCompletion" }
    };

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        _callCount++;
        
        // Simulate async operation
        await Task.Delay(10, cancellationToken);

        // Generate a mock response based on call count (step number)
        // MVP: Only click, wait, and complete actions
        var response = _callCount switch
        {
            1 => "I'll help you complete checkout on Amazon. Looking at the home page, I need to navigate to the cart. Let me click on the cart icon.\n\nClickElement('#nav-cart', 'Clicking cart icon to view cart')",
            2 => "Good, we're now on the cart page. I can see items in the cart. To proceed with checkout, I need to click the checkout button.\n\nClickElement('input[name=\"proceedToCheckout\"]', 'Clicking checkout button to proceed')",
            3 => "Perfect! We're now on the checkout page. The checkout process is complete. All items are ready for payment.\n\nComplete('Checkout process completed successfully')",
            _ => "Continuing with the task...\n\nWait(1, 'Analyzing page')"
        };

        var message = new ChatMessageContent(
            AuthorRole.Assistant,
            response,
            modelId: ModelId);

        return new List<ChatMessageContent> { message };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var messages = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        
        foreach (var message in messages)
        {
            yield return new StreamingChatMessageContent(
                role: message.Role,
                content: message.Content,
                modelId: message.ModelId,
                innerContent: message.InnerContent,
                metadata: message.Metadata);
        }
    }
}
