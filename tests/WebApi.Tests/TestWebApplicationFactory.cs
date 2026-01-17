using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Data;
using WebApi.Models;
using WebApi.Tests.Helpers;

namespace WebApi.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly bool _useTestAuth;
    private readonly Guid _testUserId;
    private readonly string _databaseName;

    public TestWebApplicationFactory(bool useTestAuth = true, Guid? testUserId = null)
    {
        _useTestAuth = useTestAuth;
        _testUserId = testUserId ?? Guid.NewGuid();
        // Use a unique database name per factory instance
        _databaseName = $"TestDatabase_{Guid.NewGuid()}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override connection string to signal test mode
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "InMemory"
            });
        });
        
        builder.ConfigureTestServices(services =>
        {
            // Remove the PostgreSQL DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }
            
            var appDbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AppDbContext));
            if (appDbContextDescriptor != null)
            {
                services.Remove(appDbContextDescriptor);
            }

            // Add in-memory database
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            // Configure test authentication if enabled
            if (_useTestAuth)
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

                services.Configure<TestAuthHandlerOptions>(options =>
                {
                    options.DefaultUserId = _testUserId;
                });
            }
        });
    }
    
    private bool _databaseSeeded = false;
    private readonly object _seedLock = new object();
    
    private void EnsureDatabaseSeeded(IServiceProvider services)
    {
        if (_databaseSeeded) return;
        
        lock (_seedLock)
        {
            if (_databaseSeeded) return;
            
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            db.Database.EnsureCreated();
            SeedTestData(db);
            
            _databaseSeeded = true;
        }
    }

    public HttpClient CreateAuthenticatedClient(Guid? userId = null)
    {
        var client = CreateClient();
        EnsureDatabaseSeeded(Services);
        
        if (_useTestAuth)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
            if (userId.HasValue)
            {
                client.DefaultRequestHeaders.Add("X-Test-UserId", userId.Value.ToString());
            }
        }
        return client;
    }

    public HttpClient CreateUnauthenticatedClient()
    {
        var client = CreateClient();
        EnsureDatabaseSeeded(Services);
        return client;
    }

    private void SeedTestData(AppDbContext context)
    {
        // Clear existing data
        context.TodoItems.RemoveRange(context.TodoItems);
        context.Tags.RemoveRange(context.Tags);
        context.Users.RemoveRange(context.Users);
        context.SaveChanges();

        // Create test users
        var user1 = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!", workFactor: 12),
            Provider = AuthProvider.Local,
            ProviderUserId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!", workFactor: 12),
            Provider = AuthProvider.Local,
            ProviderUserId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(user1, user2);
        context.SaveChanges();

        // Create test tags
        var workTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "Work",
            Color = "#FF5733",
            UserId = user1.Id
        };

        var personalTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "Personal",
            Color = "#3498DB",
            UserId = user1.Id
        };

        var urgentTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = "Urgent",
            Color = "#FF0000",
            UserId = user2.Id
        };

        context.Tags.AddRange(workTag, personalTag, urgentTag);
        context.SaveChanges();

        // Create test todos
        var todo1 = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Test Todo 1",
            Description = "First test todo",
            IsCompleted = false,
            Priority = Priority.High,
            DueDate = DateTime.UtcNow.AddDays(1),
            UserId = user1.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        todo1.Tags.Add(workTag);

        var todo2 = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Test Todo 2",
            Description = "Second test todo",
            IsCompleted = true,
            Priority = Priority.Medium,
            DueDate = DateTime.UtcNow.AddDays(2),
            UserId = user1.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var todo3 = new TodoItem
        {
            Id = Guid.NewGuid(),
            Title = "Other User Todo",
            Description = "Todo belonging to another user",
            IsCompleted = false,
            Priority = Priority.Low,
            DueDate = null,
            UserId = user2.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.TodoItems.AddRange(todo1, todo2, todo3);
        context.SaveChanges();
    }
}

public class TestAuthHandlerOptions
{
    public Guid DefaultUserId { get; set; }
}

