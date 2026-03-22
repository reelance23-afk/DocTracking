using DocTracking.Data;
using DocTracking.Client.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DocTracking.Controllers
{
    [Authorize]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> UpdateOffice(int id, [FromBody] Office office)
        {
            var existing = await _context.Offices.FindAsync(id);
            if (existing == null) return NotFound();

            var duplicate = await _context.Offices.AnyAsync(o => o.Name == office.Name && o.Id != id);
            if (duplicate) return Conflict("An office with this name already exists");
            existing.Name = office.Name;
            existing.ReceivingSchedule = office.ReceivingSchedule;
            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteOffice(int id)
        {
            var existing = await _context.Offices
                .Include(o => o.Units)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (existing == null) return NotFound();

            var hasUsers = await _context.AppUsers.AnyAsync(u => u.OfficeId == id);
            if (hasUsers)
            {
                return BadRequest("Cannot delete office that has users assigned. Please reassign users first.");
            }

            var unitIds = await _context.Units
                .Where(u => u.OfficeId == id)
                .Select(u => u.Id)
                .ToListAsync();
            var hasUsersInUnits = unitIds.Any() && await _context.AppUsers
                .AnyAsync(u => u.UnitId != null && unitIds.Contains(u.UnitId.Value));

            if (hasUsersInUnits)
            {
                return BadRequest("Cannot delete office that has users assigned to its units. Please reassign users first.");
            }

            var hasActiveDocuments = await _context.Documents.AnyAsync(d =>
                d.CurrentOfficeId == id || d.NextOfficeId == id);

            if (hasActiveDocuments)
            {
                return BadRequest("Cannot delete office that has active documents assigned. Please reassign documents first.");
            }

            var officeLogsToUpdate = await _context.DocumentLogs
                .Where(dl => dl.OfficeId == id)
                .ToListAsync();

            foreach (var log in officeLogsToUpdate)
            {
                if (string.IsNullOrEmpty(log.OfficeName))
                {
                    log.OfficeName = existing.Name;
                }
                log.OfficeId = null;
            }

          
            if (existing.Units != null && existing.Units.Any())
            {
                foreach (var unit in existing.Units)
                {
                    var unitLogsToUpdate = await _context.DocumentLogs
                        .Where(dl => dl.UnitId == unit.Id)
                        .ToListAsync();

                    foreach (var log in unitLogsToUpdate)
                    {
                        if (string.IsNullOrEmpty(log.UnitName))
                        {
                            log.UnitName = unit.Name;
                        }
                        if (string.IsNullOrEmpty(log.OfficeName))
                        {
                            log.OfficeName = existing.Name;
                        }
                        log.UnitId = null;
                    }
                }

                _context.Units.RemoveRange(existing.Units);
            }

            _context.Offices.Remove(existing);
            await _context.SaveChangesAsync();
            return Ok();
        }


    }
}
