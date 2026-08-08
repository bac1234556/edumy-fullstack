using EduMy.Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CertificatesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CertificatesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("{url}")]
        public async Task<IActionResult> GetCertificateByUrl(string url)
        {
            var certificate = await _context.Certificates
                .Include(c => c.User)
                .Include(c => c.Course)
                    .ThenInclude(course => course.Instructor)
                .FirstOrDefaultAsync(c => c.CertificateUrl == url);

            if (certificate == null) return NotFound();

            return Ok(new
            {
                certificateId = certificate.Id,
                issuedAt = certificate.IssuedAt,
                studentName = certificate.User?.FullName,
                courseName = certificate.Course?.Title,
                instructorName = certificate.Course?.Instructor?.FullName,
                certificateUrl = certificate.CertificateUrl
            });
        }
    }
}
