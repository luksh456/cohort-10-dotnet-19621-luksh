using TaskManagement.Api.Dtos;
namespace TaskManagement.Api.Services;
public interface IAuthService
{
    Task<bool> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
}