using System.Security.Claims;
using EduMy.Backend.DTOs;
using EduMy.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduMy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Student,Instructor")]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountDeletionService _deletion;
    public AccountController(IAccountDeletionService deletion) => _deletion = deletion;

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe(AccountDeleteDto dto)
    {
        if (dto.Confirmation.Trim() != "XÓA TÀI KHOẢN")
            return BadRequest(new { code = "INVALID_DELETE_CONFIRMATION", message = "Vui lòng nhập chính xác “XÓA TÀI KHOẢN”." });
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var result = await _deletion.DeleteAsync(userId, userId, true);
        return StatusCode(result.StatusCode, result);
    }
}
