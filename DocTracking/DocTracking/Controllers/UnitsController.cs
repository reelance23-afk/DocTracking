using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace DocTracking.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("api")]
    public class UnitsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UnitsController> _logger;
        public UnitsController(ApplicationDbContext context, ILogger<UnitsController> logger)
        {
            _context = context;
            _logger = logger;
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
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AddUnit] Failed");
                return StatusCode(500, "Failed to save unit.");
            }
            return Ok(unit);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUnit(int id, [FromBody] Unit unit)
        {
            var existing = await _context.Units.FindAsync(id);
            if (existing == null) return NotFound();

            var duplicate = await _context.Units.AnyAsync(o => o.Name == unit.Name && o.OfficeId == unit.OfficeId && o.Id != id);
            if (duplicate) return Conflict("An Unit with this name already exists");
            existing.Name = unit.Name;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UpdateUnit] Failed");
                return StatusCode(500, "Failed to update unit.");
            }
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUnit(int id)
        {
            var existing = await _context.Units
                .Include(u => u.Office)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (existing == null) return NotFound();

            var hasUsers = await _context.AppUsers.AnyAsync(u => u.UnitId == id);
            if (hasUsers)
            {
                return BadRequest("Cannot delete unit that has users assigned. Please reassign users first.");
            }

            var hasActiveDocuments = await _context.Documents.AnyAsync(d =>
                d.CurrentUnitId == id || d.NextUnitId == id);
            if (hasActiveDocuments)
            {
                return BadRequest("Cannot delete unit that has active documents assigned. Please reassign documents first.");
            }

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.DocumentLogs
                    .Where(dl => dl.UnitId == id && string.IsNullOrEmpty(dl.UnitName))
                    .ExecuteUpdateAsync(dl => dl
                        .SetProperty(x => x.UnitId, (int?)null)
                        .SetProperty(x => x.UnitName, existing.Name)
                        .SetProperty(x => x.OfficeName, existing.Office!.Name));

                _context.Units.Remove(existing);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[DeleteUnit] Failed");
                return StatusCode(500, "Failed to delete unit.");
            }
            return Ok();
        }  
    }
}
