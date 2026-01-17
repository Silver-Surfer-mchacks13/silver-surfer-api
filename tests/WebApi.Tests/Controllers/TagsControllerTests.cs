using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Data;
using WebApi.DTOs.Tags;
using WebApi.DTOs.Todos;
using WebApi.Models;
using WebApi.Tests.Helpers;

namespace WebApi.Tests.Controllers;

[TestFixture]
public class TagsControllerTests
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
    public async Task GetAllTags_ReturnsOkWithTags()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/tags");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<List<TagDto>>();
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThanOrEqualTo(2); // Should have at least 2 tags from seed data
    }

    [Test]
    public async Task GetTagById_WithValidId_ReturnsOk()
    {
        // Arrange - Get a tag ID from the database
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = context.Tags.First(t => t.UserId == _testUserId);

        // Act
        var response = await _client.GetAsync($"/api/v1/tags/{tag.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TagDto>();
        result.Should().NotBeNull();
        result!.Id.Should().Be(tag.Id);
        result.Name.Should().Be(tag.Name);
        result.Color.Should().Be(tag.Color);
    }

    [Test]
    public async Task GetTagById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/tags/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetTagById_OtherUsersTag_ReturnsNotFound()
    {
        // Arrange - Get a tag from another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherUser = context.Users.First(u => u.Id != _testUserId);
        var otherUserTag = context.Tags.First(t => t.UserId == otherUser.Id);

        // Act
        var response = await _client.GetAsync($"/api/v1/tags/{otherUserTag.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateTag_WithValidData_ReturnsCreated()
    {
        // Arrange
        var request = new CreateTagRequest
        {
            Name = "Urgent",
            Color = "#FF0000"
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TagDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be(request.Name);
        result.Color.Should().Be(request.Color);
    }

    [Test]
    public async Task CreateTag_WithDuplicateName_ReturnsBadRequest()
    {
        // Arrange - Work tag already exists in seed data
        var request = new CreateTagRequest
        {
            Name = "Work",
            Color = "#00FF00"
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task CreateTag_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTagRequest
        {
            Name = "",
            Color = "#FF0000"
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateTag_WithInvalidColor_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTagRequest
        {
            Name = "NewTag",
            Color = "invalid-color"
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateTag_WithValidData_ReturnsOk()
    {
        // Arrange - Get existing tag
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tag = context.Tags.First(t => t.UserId == _testUserId);

        var request = new UpdateTagRequest
        {
            Name = "Updated Work",
            Color = "#00FF00"
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/tags/{tag.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TagDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be(request.Name);
        result.Color.Should().Be(request.Color);
    }

    [Test]
    public async Task UpdateTag_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateTagRequest
        {
            Name = "Updated Tag",
            Color = "#00FF00"
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/tags/{nonExistentId}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateTag_OtherUsersTag_ReturnsNotFound()
    {
        // Arrange - Get a tag from another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherUser = context.Users.First(u => u.Id != _testUserId);
        var otherUserTag = context.Tags.First(t => t.UserId == otherUser.Id);

        var request = new UpdateTagRequest
        {
            Name = "Trying to update",
            Color = "#00FF00"
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/tags/{otherUserTag.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateTag_WithDuplicateName_ReturnsBadRequest()
    {
        // Arrange - Get a tag and try to rename it to another existing tag's name
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tags = context.Tags.Where(t => t.UserId == _testUserId).Take(2).ToList();

        var request = new UpdateTagRequest
        {
            Name = tags[1].Name, // Try to use the second tag's name
            Color = "#00FF00"
        };

        // Act
        var response = await _client.PutAsJsonSnakeCaseAsync($"/api/v1/tags/{tags[0].Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task DeleteTag_WithValidId_ReturnsNoContent()
    {
        // Arrange - Create a tag to delete
        var createRequest = new CreateTagRequest
        {
            Name = "Tag to delete",
            Color = "#FF00FF"
        };
        var createResponse = await _client.PostAsJsonSnakeCaseAsync("/api/v1/tags", createRequest);
        var createdTag = await createResponse.Content.ReadFromJsonSnakeCaseAsync<TagDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tags/{createdTag!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's deleted
        var getResponse = await _client.GetAsync($"/api/v1/tags/{createdTag.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTag_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tags/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTag_OtherUsersTag_ReturnsNotFound()
    {
        // Arrange - Get a tag from another user
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherUser = context.Users.First(u => u.Id != _testUserId);
        var otherUserTag = context.Tags.First(t => t.UserId == otherUser.Id);

        // Act
        var response = await _client.DeleteAsync($"/api/v1/tags/{otherUserTag.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task TagTodoAssociation_CreateTodoWithTags_TagsAreAssociated()
    {
        // Arrange - Get tag IDs
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tags = context.Tags.Where(t => t.UserId == _testUserId).Take(2).ToList();

        var todoRequest = new CreateTodoRequest
        {
            Title = "Todo with multiple tags",
            Description = "Testing tag associations",
            Priority = Priority.Medium,
            TagIds = tags.Select(t => t.Id).ToList()
        };

        // Act
        var response = await _client.PostAsJsonSnakeCaseAsync("/api/v1/todos", todoRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        result.Should().NotBeNull();
        result!.Tags.Should().HaveCount(2);
        result.Tags.Select(t => t.Id).Should().Contain(tags[0].Id);
        result.Tags.Select(t => t.Id).Should().Contain(tags[1].Id);
    }

    [Test]
    public async Task TagTodoAssociation_DeleteTag_TodosNoLongerHaveTag()
    {
        // Arrange - Create a tag and a todo with that tag
        var tagRequest = new CreateTagRequest
        {
            Name = "TempTag",
            Color = "#123456"
        };
        var tagResponse = await _client.PostAsJsonSnakeCaseAsync("/api/v1/tags", tagRequest);
        var createdTag = await tagResponse.Content.ReadFromJsonSnakeCaseAsync<TagDto>();

        var todoRequest = new CreateTodoRequest
        {
            Title = "Todo with temp tag",
            Priority = Priority.Low,
            TagIds = new List<Guid> { createdTag!.Id }
        };
        var todoResponse = await _client.PostAsJsonSnakeCaseAsync("/api/v1/todos", todoRequest);
        var createdTodo = await todoResponse.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();

        // Act - Delete the tag
        var deleteResponse = await _client.DeleteAsync($"/api/v1/tags/{createdTag.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify the todo no longer has the tag
        var getTodoResponse = await _client.GetAsync($"/api/v1/todos/{createdTodo!.Id}");
        var updatedTodo = await getTodoResponse.Content.ReadFromJsonSnakeCaseAsync<TodoItemDto>();
        updatedTodo!.Tags.Should().NotContain(t => t.Id == createdTag.Id);
    }
}

