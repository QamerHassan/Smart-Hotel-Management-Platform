using SmartHotel.Domain.Entities;

namespace SmartHotel.Domain.Interfaces;

public interface IAuthService
{
    string GenerateJwtToken(User user);
    string GenerateRefreshToken();
    bool VerifyPassword(string password, string passwordHash);
    string HashPassword(string password);
}
