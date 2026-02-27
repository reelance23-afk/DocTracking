using DocTracking.Data;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfficesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OfficesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<List<Office>>> AddOffice([FromBody] Office office)
        {
            _context.Offices.Add(office);
            await _context.SaveChangesAsync();
            return Ok(office);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Office>>> GetOffices()
        {
            return await _context.Offices
                .Include(o => o.Units)
                .OrderBy(o => o.Name)
                .ToListAsync();
        }
    }
}
