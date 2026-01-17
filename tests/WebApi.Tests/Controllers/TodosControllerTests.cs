using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Data;
using WebApi.DTOs.Todos;
using WebApi.Models;
using WebApi.Tests.Helpers;

namespace WebApi.Tests.Controllers;

[TestFixture]
public class TodosControllerTests
{
    private TestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _testUserId;

    [SetUp]
    public void Setup()
    {
        _testUserId = Guid.NewGuid();
        _factory = new TestWebApplicationFactory(useTestAuth: true, testUserId: _testUserId);
        _client = _factory.CreateAuthenticatedClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetAllTodos_ReturnsOkWithTodos()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/todos");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<List<TodoItemDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(2); // Should have at least 2 todos from seed data
    }

    [Test]
    public async Task GetAllTodos_FilterByCompleted_ReturnsOnlyCompletedTodos()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/todos?isCompleted=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<List<TodoItemDto>>();
        result.Should().NotBeNull();
        result.Should().OnlyContain(t => t.IsCompleted == true);
    }

    [Test]
    public async Task GetAllTodos_FilterByPending_ReturnsOnlyPendingTodos()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/todos?isCompleted=false");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<List<TodoItemDto>>();
        result.Should().NotBeNull();
        result.Should().OnlyContain(t => t.IsCompleted == false);
    }

    [Test]
    public async Task GetAllTodos_FilterByPriority_ReturnsOnlyMatchingPriority()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/todos?priority=2"); // High priority

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<List<TodoItemDto>>();
        result.Should().NotBeNull();
        result.Should().OnlyContain(t => t.Priority == Priority.High);
    }

    [Test]
    public async Task GetAllTodos_FilterByTag_ReturnsOnlyTodosWithTag()
    {
        // Arrange - Get the Work tag ID
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var workTag = context.Tags.First(t => t.Name == "Work" && t.UserId == _testUserId);

        // Act
        var response = await _client.GetAsync($"/api/v1/todos?tagId={workTag.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<List<TodoItemDto>>();
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
        result.Should().OnlyContain(t => t.Tags.Any(tag => tag.Id == workTag.Id));
    }

    [Test]
    public async Task GetTodoById_WithValidId_ReturnsOk()
    {
        // Arrange - Get a todo ID from the database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var todo = context.TodoItems.First(t => t.UserId == _testUserId);

        // Act
        var response = await _client.GetAsync($"/api/v1/todos/{todo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(todo.Id);
        result.Title.Should().Be(todo.Title);
    }

    [Test]
    public async Task GetTodoById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/todos/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTodoById_OtherUsersTodo_ReturnsNotFound()
    {
        // Arrange - Get a todo from another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherUserTodo = context.TodoItems.First(t => t.UserId != _testUserId);

        // Act
        var response = await _client.GetAsync($"/api/v1/todos/{otherUserTodo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateTodo_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new CreateTodoRequest
        {
            Title = "New Test Todo",
            Description = "This is a new todo for testing",
            Priority = Priority.Medium,
            DueDate = DateTime.UtcNow.AddDays(3)
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/todos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        result.Should().NotBeNull();
        result!.Title.Should().Be(request.Title);
        result.Description.Should().Be(request.Description);
        result.Priority.Should().Be(request.Priority);
        result.IsCompleted.Should().BeFalse();
    }

    [Test]
    public async Task CreateTodo_WithTags_ReturnsCreatedWithTags()
    {
        // Arrange - Get tag IDs
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tags = context.Tags.Where(t => t.UserId == _testUserId).Take(2).ToList();

        var request = new CreateTodoRequest
        {
            Title = "Todo with Tags",
            Description = "Testing tags",
            Priority = Priority.Low,
            TagIds = tags.Select(t => t.Id).ToList()
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/todos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        result.Should().NotBeNull();
        result!.Tags.Should().HaveCount(2);
    }

    [Test]
    public async Task CreateTodo_WithEmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTodoRequest
        {
            Title = "",
            Description = "Testing validation",
            Priority = Priority.Medium
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/todos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateTodo_WithValidData_ReturnsOk()
    {
        // Arrange - Get existing todo
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var todo = context.TodoItems.First(t => t.UserId == _testUserId);

        var request = new UpdateTodoRequest
        {
            Title = "Updated Title",
            Description = "Updated description",
            Priority = Priority.High,
            DueDate = DateTime.UtcNow.AddDays(5)
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/todos/{todo.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        result.Should().NotBeNull();
        result!.Title.Should().Be(request.Title);
        result.Description.Should().Be(request.Description);
        result.Priority.Should().Be(request.Priority);
    }

    [Test]
    public async Task UpdateTodo_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateTodoRequest
        {
            Title = "Updated Title",
            Priority = Priority.High
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/todos/{nonExistentId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateTodo_OtherUsersTodo_ReturnsNotFound()
    {
        // Arrange - Get a todo from another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherUserTodo = context.TodoItems.First(t => t.UserId != _testUserId);

        var request = new UpdateTodoRequest
        {
            Title = "Trying to update",
            Priority = Priority.High
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/todos/{otherUserTodo.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ToggleTodoCompletion_CompletesTodo_ReturnsOk()
    {
        // Arrange - Get a pending todo
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var todo = context.TodoItems.First(t => t.UserId == _testUserId && !t.IsCompleted);
        var originalCompletionStatus = todo.IsCompleted;

        // Act
        var response = await _client.PatchAsync($"/api/v1/todos/{todo.Id}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        result.Should().NotBeNull();
        result!.IsCompleted.Should().Be(!originalCompletionStatus);
    }

    [Test]
    public async Task ToggleTodoCompletion_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.PatchAsync($"/api/v1/todos/{nonExistentId}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTodo_WithValidId_ReturnsNoContent()
    {
        // Arrange - Create a todo to delete
        var createRequest = new CreateTodoRequest
        {
            Title = "Todo to delete",
            Priority = Priority.Low
        };
        var createResponse = await _client.PostAsJsonSnakeCaseAsync("/api/v1/todos", createRequest);
        var createdTodo = await createResponse.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/todos/{createdTodo!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _client.GetAsync($"/api/v1/todos/{createdTodo.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTodo_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/todos/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTodo_OtherUsersTodo_ReturnsNotFound()
    {
        // Arrange - Get a todo from another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherUserTodo = context.TodoItems.First(t => t.UserId != _testUserId);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/todos/{otherUserTodo.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

