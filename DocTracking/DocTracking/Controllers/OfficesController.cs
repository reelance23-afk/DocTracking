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
            var exists = await _context.Offices.AnyAsync(o => o.Name == office.Name);
            if (exists) return Conflict("An office with this name already exists");

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

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateOffice(int id, [FromBody] Office office)
        {
            var existing = await _context.Offices.FindAsync(id);
            if (existing == null) return NotFound();

            var duplicate = await _context.Offices.AnyAsync(o => o.Name == office.Name && o.Id != id);
            if (duplicate) return Conflict("An office with this name already exists");
            existing.Name = office.Name;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        public async Task <IActionResult> DeleteOffice(int id)
        {
            var existing = await _context.Offices.FindAsync(id);
            if (existing == null) return NotFound();
            _context.Offices.Remove(existing);
            await _context.SaveChangesAsync();
            return Ok();

        }
    }
}
