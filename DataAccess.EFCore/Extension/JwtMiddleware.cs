using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DataAccess.EFCore.Extension
{
    public class JwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider; // Service for refreshing the token
        public JwtMiddleware(RequestDelegate next, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _next = next;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
        }

        public async Task Invoke(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            // var refreshToken = context.Request.Headers["Refresh-Token"].FirstOrDefault();            
            if (token != null)
            {
                try
                {
                    // Check if the JWT token is valid
                    var user = ValidateJwtToken(token);
                    if (user != null)
                    {
                        context.Items["User"] = user; // Store user information in context if valid
                    }
                }
                catch (SecurityTokenExpiredException)
                {
                    // If token expired, get the refresh token from Cookies       
                    var refreshToken = context.Request.Cookies["refreshToken"]; // Get the refresh token from cookie
                    if (refreshToken != null)
                    {
                        // Refresh the token if the refresh token is valid
                        try
                        {
                            // Create a new scope to access IAuthService
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
                                var newJwtToken = await authService.RefreshToken(refreshToken);

                                // Send new JWT in the response header to the client
                                context.Response.Headers["Authorization"] = "Bearer " + newJwtToken;

                                // Continue the request with the new token
                                var user = ValidateJwtToken(newJwtToken);
                                context.Items["User"] = user;
                            }
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            // If refresh token is invalid or expired, return an error
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsync(ex.Message);
                            return;
                        }
                    }
                    else
                    {
                        // If no refresh token is present, return token expired error
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsync("Token expired. Refresh token is required.");
                        return;
                    }
                }
            }

            // Pass the request to the next middleware
            await _next(context);
        }

        // Method to validate the JWT token
        private ClaimsPrincipal ValidateJwtToken(string token)
        {
            var keyValue = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(keyValue))
            {
                throw new Exception("Jwt:Key is not configured in appsettings.json.");
            }
            var key = Encoding.ASCII.GetBytes(keyValue);

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero // No allowance for clock skew
                }, out var validatedToken);

                return principal;
            }
            catch (Exception)
            {
                throw new SecurityTokenExpiredException();
            }
        }
    }
}