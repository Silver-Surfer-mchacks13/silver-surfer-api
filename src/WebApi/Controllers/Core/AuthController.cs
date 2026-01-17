using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Web.Common.DTOs;
using Web.Common.DTOs.Auth;
using WebApi.Controllers.Utils;
using WebApi.Exceptions;
using WebApi.Models;
using WebApi.Services.Auth;

namespace WebApi.Controllers.Core;

/// <summary>
/// Handles user authentication and registration
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("auth")]
public class AuthController(
    AuthService authService, 
    PasswordResetService passwordResetService) 
    : ControllerBase
{
    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">User registration information including email and password</param>
    /// <returns>Authentication response with user details, access token, and refresh token</returns>
    /// <response code="201">User successfully registered</response>
    /// <response code="400">Invalid input or user already exists</response>
    /// <remarks>
    /// Password requirements:
    /// - Minimum 12 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one number
    /// - At least one special character
    ///
    /// Sample request:
    ///
    ///     POST /api/v1/auth/register
    ///     {
    ///        "email": "john@example.com",
    ///        "password": "SecurePass123!"
    ///     }
    ///
    /// </remarks>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await authService.RegisterAsync(request);
            return CreatedAtAction(nameof(GetCurrentUser), response);
        }
        catch (ValidationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
        catch (ConflictException ex)
        {
            return this.ConflictError(ex.Message);
        }
    }

    /// <summary>
    /// Authenticate an existing user
    /// </summary>
    /// <param name="request">Login credentials (email and password)</param>
    /// <returns>Authentication response with user details, access token, and refresh token</returns>
    /// <response code="200">Login successful</response>
    /// <response code="401">Invalid credentials</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/login
    ///     {
    ///        "email": "john@example.com",
    ///        "password": "SecurePass123!"
    ///     }
    ///
    /// </remarks>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.UnauthorizedError(ex.Message);
        }
    }

    /// <summary>
    /// Refresh access token using a refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New authentication response with new access and refresh tokens</returns>
    /// <response code="200">Token refreshed successfully</response>
    /// <response code="400">Invalid refresh token</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/refresh
    ///     {
    ///        "refreshToken": "base64_encoded_refresh_token"
    ///     }
    ///
    /// </remarks>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
    }

    /// <summary>
    /// Get current authenticated user's information
    /// </summary>
    /// <returns>Current user details</returns>
    /// <response code="200">User information retrieved successfully</response>
    /// <response code="401">User not authenticated</response>
    /// <response code="404">User not found</response>
    /// <remarks>
    /// Requires authentication. Include the JWT token in the Authorization header:
    /// 
    ///     Authorization: Bearer {your_token}
    /// 
    /// </remarks>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return this.UnauthorizedError("Invalid token");
        }

        var user = await authService.GetUserByIdAsync(userId);
        
        if (user == null)
        {
            return this.NotFoundError("User not found");
        }

        return Ok(user);
    }

    /// <summary>
    /// Request a password reset email
    /// </summary>
    /// <param name="request">Email address for password reset</param>
    /// <returns>Success response (always returns 200 for security reasons)</returns>
    /// <response code="200">Password reset email sent if email exists</response>
    /// <remarks>
    /// For security reasons, this endpoint always returns 200 OK even if the email doesn't exist.
    /// This prevents email enumeration attacks.
    ///
    /// Sample request:
    ///
    ///     POST /api/v1/auth/password-reset/request
    ///     {
    ///        "email": "user@example.com"
    ///     }
    ///
    /// </remarks>
    [HttpPost("password-reset/request")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return this.BadRequestError("Email is required");
        }

        await passwordResetService.CreatePasswordResetRequest(request.Email);
        return Ok(new { message = "If an account with that email exists, a password reset link has been sent." });
    }
    
    /// <summary>
    /// Confirm password reset with token and new password
    /// </summary>
    /// <param name="request">Password reset confirmation with token and new password</param>
    /// <returns>Success response</returns>
    /// <response code="200">Password reset successful</response>
    /// <response code="400">Invalid token or password validation failed</response>
    /// <remarks>
    /// Sample request:
    ///
    ///     POST /api/v1/auth/password-reset/confirm
    ///     {
    ///        "token": "base64_encoded_token",
    ///        "newPassword": "NewSecurePass123!"
    ///     }
    ///
    /// Password requirements:
    /// - Minimum 12 characters
    /// - At least one uppercase letter
    /// - At least one lowercase letter
    /// - At least one number
    /// - At least one special character
    ///
    /// </remarks>
    [HttpPost("password-reset/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ConfirmPasswordReset([FromBody] ConfirmPasswordResetRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return this.BadRequestError("Token is required");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return this.BadRequestError("New password is required");
        }

        var result = await passwordResetService.PerformPasswordResetRequest(request.Token, request.NewPassword);
        
        if (!result.IsSuccess)
        {
            return this.BadRequestError(result.ErrorMessage ?? "Invalid or expired token");
        }

        return Ok(new { message = "Password has been reset successfully. You can now log in with your new password." });
    }

    /// <summary>
    /// Authenticate with OAuth provider using ID token or authorization code
    /// </summary>
    /// <param name="request">OAuth login request with provider name and either ID token or authorization code</param>
    /// <returns>Authentication response with user details, access token, and refresh token</returns>
    /// <response code="200">Login successful</response>
    /// <response code="400">Invalid request (missing or invalid parameters)</response>
    /// <response code="401">Token validation failed</response>
    /// <response code="409">Account conflict</response>
    /// <remarks>
    /// This endpoint validates OAuth tokens from various providers.
    /// 
    /// For Google and Microsoft (ID token flow):
    /// - The frontend should obtain the ID token from the OAuth provider and send it here.
    /// - Sample request:
    ///     POST /api/v1/auth/oauth
    ///     {
    ///        "provider": "Google",
    ///        "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ij..."
    ///     }
    ///
    /// For GitHub (authorization code flow):
    /// - The frontend should send the authorization code received from GitHub.
    /// - Sample request:
    ///     POST /api/v1/auth/oauth
    ///     {
    ///        "provider": "GitHub",
    ///        "authorizationCode": "abc123...",
    ///        "redirectUri": "https://localhost:5000/login?provider=GitHub"
    ///     }
    ///
    /// Supported providers: Google, Microsoft, GitHub
    ///
    /// </remarks>
    [HttpPost("oauth")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> OAuthLogin([FromBody] OAuthLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Provider))
        {
            return this.BadRequestError("Provider is required");
        }

        if (string.IsNullOrWhiteSpace(request.IdToken) && string.IsNullOrWhiteSpace(request.AuthorizationCode))
        {
            return this.BadRequestError("Either IdToken or AuthorizationCode is required");
        }

        if (!Enum.TryParse<AuthProvider>(request.Provider, ignoreCase: true, out var provider))
        {
            return this.BadRequestError($"Invalid provider '{request.Provider}'. Supported providers: {string.Join(", ", Enum.GetNames<AuthProvider>().Where(p => p != "Local"))}");
        }

        if (provider == AuthProvider.Local)
        {
            return this.BadRequestError("Local provider is not supported for OAuth login");
        }

        try
        {
            var response = await authService.LoginWithOAuthAsync(
                provider, 
                idToken: request.IdToken, 
                authorizationCode: request.AuthorizationCode, 
                redirectUri: request.RedirectUri);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.UnauthorizedError(ex.Message);
        }
        catch (ConflictException ex)
        {
            return this.ConflictError(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return this.BadRequestError(ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return this.BadRequestError(ex.Message);
        }
    }

    public record PasswordResetRequestDto(string Email);
    public record ConfirmPasswordResetRequestDto(string Token, string NewPassword);
}
