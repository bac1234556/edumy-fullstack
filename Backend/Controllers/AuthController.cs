using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                return BadRequest(new { message = "Email already in use." });
            }

            var user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                Role = "Student",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            try
            {
                if (loginDto == null || string.IsNullOrEmpty(loginDto.Email) || string.IsNullOrEmpty(loginDto.Password))
                {
                    return BadRequest(new { message = "Tài khoản hoặc mật khẩu không chính xác" });
                }

                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == loginDto.Email);
                if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash))
                {
                    return BadRequest(new { message = "Tài khoản hoặc mật khẩu không chính xác" });
                }
                if (user.IsDeleted) return AccountDeleted();
                if (!user.IsActive) return AccountInactive();

                var token = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();
                
                user.RefreshTokens.Add(new RefreshToken
                {
                    Token = refreshToken,
                    Expires = DateTime.UtcNow.AddDays(GetRefreshTokenDays()),
                    Created = DateTime.UtcNow
                });
                
                await _context.SaveChangesAsync();

                SetTokenCookie(refreshToken);

                return Ok(new AuthResponseDto { 
                    Token = token, 
                    Email = user.Email, 
                    FullName = user.FullName, 
                    Role = user.Role, 
                    Message = "Login successful." 
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Login Exception] {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return BadRequest(new { message = "Tài khoản hoặc mật khẩu không chính xác" });
            }
        }
        
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { message = "Token is required." });

            var user = await _context.Users
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken));

            if (user == null)
                return Unauthorized(new { message = "Invalid token." });
            if (user.IsDeleted) return AccountDeleted();
            if (!user.IsActive) return AccountInactive();

            var refreshTokenEntity = user.RefreshTokens.Single(x => x.Token == refreshToken);
            if (!refreshTokenEntity.IsActive)
                return Unauthorized(new { message = "Token is expired or revoked." });

            // Revoke current token
            refreshTokenEntity.Revoked = DateTime.UtcNow;
            
            // Generate new tokens
            var newJwtToken = GenerateJwtToken(user);
            var newRefreshToken = GenerateRefreshToken();
            
            refreshTokenEntity.ReplacedByToken = newRefreshToken;
            
            user.RefreshTokens.Add(new RefreshToken
            {
                Token = newRefreshToken,
                Expires = DateTime.UtcNow.AddDays(GetRefreshTokenDays()),
                Created = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            
            SetTokenCookie(newRefreshToken);

            return Ok(new AuthResponseDto { 
                Token = newJwtToken, 
                Email = user.Email, 
                FullName = user.FullName, 
                Role = user.Role, 
                Message = "Token refreshed successfully." 
            });
        }
        
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var user = await _context.Users.Include(u => u.RefreshTokens)
                    .SingleOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == refreshToken));
                var entity = user?.RefreshTokens.SingleOrDefault(x => x.Token == refreshToken);
                if (entity?.IsActive == true)
                {
                    entity.Revoked = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            DeleteTokenCookie();
            return Ok(new { message = "Token revoked." });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto googleLoginDto)
        {
            try
            {
                var payload = await Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync(googleLoginDto.Token);
                if (payload == null) return BadRequest("Invalid Google token.");

                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == payload.Email);
                if (user == null)
                {
                    // Register user automatically
                    user = new User
                    {
                        FullName = payload.Name,
                        Email = payload.Email,
                        PasswordHash = "", // No password since they login with Google
                        Role = "Student",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                if (user.IsDeleted) return AccountDeleted();
                if (!user.IsActive) return AccountInactive();

                var token = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();
                
                user.RefreshTokens.Add(new RefreshToken
                {
                    Token = refreshToken,
                    Expires = DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                
                SetTokenCookie(refreshToken);

                return Ok(new AuthResponseDto { 
                    Token = token, 
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = user.Role,
                    Message = "Google login successful." 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Google token validation failed.", error = ex.Message });
            }
        }

        [HttpGet("google-login")]
        public IActionResult GoogleLoginChallenge()
        {
            var clientId = _configuration["Authentication:Google:ClientId"];
            if (string.IsNullOrEmpty(clientId) || clientId.Contains("your-google-client-id") || clientId == "mock-google-client-id")
            {
                return BadRequest(new { 
                    message = "Google Sign-In is not configured. Configure ClientId, ClientSecret and the server callback /api/auth/google-response." 
                });
            }

            var properties = new AuthenticationProperties { RedirectUri = "/api/auth/google-response" };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("google-response")]
        public async Task<IActionResult> GoogleResponse()
        {
            try
            {
                var clientId = _configuration["Authentication:Google:ClientId"];
                if (string.IsNullOrEmpty(clientId) || clientId.Contains("your-google-client-id") || clientId == "mock-google-client-id")
                {
                    return BadRequest(new { message = "Google Sign-In is not properly configured on this server." });
                }

                var authenticateResult = await HttpContext.AuthenticateAsync("ExternalCookie");
                if (!authenticateResult.Succeeded)
                {
                    return BadRequest(new { message = "Google authentication failed. No external credentials found." });
                }

                var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email);
                var name = authenticateResult.Principal.FindFirstValue(ClaimTypes.Name) ?? email;

                if (string.IsNullOrEmpty(email))
                {
                    return BadRequest(new { message = "Email claim not found from Google claims." });
                }

                await HttpContext.SignOutAsync("ExternalCookie");

                var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    user = new User
                    {
                        FullName = name,
                        Email = email,
                        PasswordHash = "", // External login has no local password hash
                        Role = "Student", // Default is Student as required
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                if (user.IsDeleted) return AccountDeleted();
                if (!user.IsActive) return AccountInactive();

                var token = GenerateJwtToken(user);
                var refreshToken = GenerateRefreshToken();

                user.RefreshTokens.Add(new RefreshToken
                {
                    Token = refreshToken,
                    Expires = DateTime.UtcNow.AddDays(7),
                    Created = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                SetTokenCookie(refreshToken);

                var frontendBaseUrl = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
                return Redirect($"{frontendBaseUrl}/login-success?token={Uri.EscapeDataString(token)}");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "An error occurred during Google authentication callback.", error = ex.Message });
            }
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);
            
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("role", user.Role),
                new Claim("unique_name", user.FullName)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(GetAccessTokenMinutes()),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        
        private int GetRefreshTokenDays()
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            return int.TryParse(jwtSettings["RefreshTokenDays"], out var days) ? days : 30;
        }

        private int GetAccessTokenMinutes()
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            return int.TryParse(jwtSettings["AccessTokenMinutes"], out var minutes) ? minutes : 120;
        }
        
        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
        
        private void SetTokenCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(GetRefreshTokenDays()),
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/"
            };
            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }

        private void DeleteTokenCookie() => Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = Request.IsHttps,
            Path = "/"
        });

        private ObjectResult AccountInactive() => StatusCode(StatusCodes.Status403Forbidden, new
        {
            code = "ACCOUNT_INACTIVE",
            message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.",
            adminEmail = _configuration["Support:AdminEmail"]
        });

        private ObjectResult AccountDeleted() => StatusCode(StatusCodes.Status403Forbidden, new
        {
            code = "ACCOUNT_DELETED",
            message = "Tài khoản đã được xóa."
        });

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                return Ok(new { message = "If the email is registered, a password reset token has been generated." });
            }

            var token = Guid.NewGuid().ToString("N")[..8].ToUpper();
            user.ResetToken = token;
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Nếu email đã đăng ký, hướng dẫn đặt lại mật khẩu sẽ được gửi." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == model.Email && u.ResetToken == model.Token);
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Invalid or expired password reset token." });
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully. You can now login with your new password." });
        }
    }
}
