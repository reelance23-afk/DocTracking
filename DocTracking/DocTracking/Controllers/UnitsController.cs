using DocTracking.Data;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;

namespace DocTracking.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UnitsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UnitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Unit>>> GetUnits()
        {
            return await _context.Units
                .Include(u => u.Office)
                .OrderBy(u => u.Name)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Unit>> AddUnit([FromBody] Unit unit)
        {
            var exists = await _context.Units.AnyAsync(u => u.Name == unit.Name && u.OfficeId == unit.OfficeId);
            if (exists) return Conflict("A unit with this name already exists in this office");

            _context.Units.Add(unit);
            await _context.SaveChangesAsync();
            return Ok(unit);
        }

        [HttpPut("{id}")]
        public async Task <IActionResult> UpdateUnit(int id, [FromBody] Unit unit)
        {
            var existing = await _context.Units.FindAsync(id);
            if (existing == null) return NotFound();

            var duplicate = await _context.Units.AnyAsync(o => o.Name == unit.Name && o.Id != id);
            if (duplicate) return Conflict("An Unit with this name already exists");
            existing.Name = unit.Name;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task <IActionResult> DeleteUnit(int id)
        {
            var existing = await _context.Units.FindAsync(id);
            if (existing == null) return NotFound();
            _context.Units.Remove(existing);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
