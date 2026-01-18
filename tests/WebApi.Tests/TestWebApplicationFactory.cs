using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using WebApi.Data;
using WebApi.Models;
using WebApi.Services.Agent;
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
            // Add test Gemini options to prevent validation errors
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["Gemini:ProjectId"] = "test-project",
                ["Gemini:Location"] = "us-central1",
                ["Gemini:Model"] = "gemini-2.0-flash",
                ["Gemini:MaxTokens"] = "65536",
                ["Gemini:Temperature"] = "0.7"
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

            // Mock the Vertex AI chat completion service for testing
            // Remove the real service registration
            var vertexAIServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(VertexAIChatCompletionService));
            if (vertexAIServiceDescriptor != null)
            {
                services.Remove(vertexAIServiceDescriptor);
            }

            // Remove the Kernel registration that depends on VertexAIChatCompletionService
            var kernelDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Kernel));
            if (kernelDescriptor != null)
            {
                services.Remove(kernelDescriptor);
            }

            // Register mock chat completion service
            services.AddSingleton<IChatCompletionService, MockChatCompletionService>();

            // Re-register Kernel with mock service
            services.AddScoped<Kernel>(sp =>
            {
                var kernelBuilder = Kernel.CreateBuilder();
                var chatCompletionService = sp.GetRequiredService<IChatCompletionService>();
                kernelBuilder.Services.AddSingleton(chatCompletionService);
                return kernelBuilder.Build();
            });
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
    }
}

public class TestAuthHandlerOptions
{
    public Guid DefaultUserId { get; set; }
}

