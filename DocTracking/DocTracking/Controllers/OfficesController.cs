using DocTracking.Client.Models;
using DocTracking.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DocTracking.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    [EnableRateLimiting("api")]
    public class OfficesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OfficesController> _logger;

        public OfficesController(ApplicationDbContext context, ILogger<OfficesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Office>> AddOffice([FromBody] Office office)
        {
            var exists = await _context.Offices.AnyAsync(o => o.Name == office.Name);
            if (exists) return Conflict("An office with this name already exists");

            _context.Offices.Add(office);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AddOffice] Failed");
                return StatusCode(500, "Failed to save office.");
            }
            return Ok(office);
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<Office>>> GetOffices(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? search = null)
        {
            var query = _context.Offices
                .Include(o => o.Units)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(o => o.Name != null && o.Name.Contains(search));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(o => o.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var unitIds = items.SelectMany(o => o.Units ?? new List<Unit>()).Select(u => u.Id).ToList();
            var officeIds = items.Select(o => o.Id).ToList();

            var unitWorkers = await _context.AppUsers
                .Where(u => u.UnitId != null && unitIds.Contains(u.UnitId.Value))
                .GroupBy(u => u.UnitId!.Value)
                .Select(g => new { UnitId = g.Key, Count = g.Count() })
                .ToListAsync();

            var officeWorkers = await _context.AppUsers
                .Where(u => u.UnitId == null && u.OfficeId != null && officeIds.Contains(u.OfficeId.Value))
                .GroupBy(u => u.OfficeId!.Value)
                .Select(g => new { OfficeId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var office in items)
            {
                var officeUnitIds = (office.Units ?? new List<Unit>()).Select(u => u.Id).ToHashSet();
                var fromUnits = unitWorkers.Where(u => officeUnitIds.Contains(u.UnitId)).Sum(u => u.Count);
                var fromOffice = officeWorkers.FirstOrDefault(o => o.OfficeId == office.Id)?.Count ?? 0;
                office.WorkerCount = fromUnits + fromOffice;
            }

            return Ok(new PagedResult<Office> { Items = items, TotalCount = total });
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<Office>> GetOffice(int id)
        {
            var office = await _context.Offices
                .Include(o => o.Units)
                .FirstOrDefaultAsync(o => o.Id == id);
            return office == null ? NotFound() : Ok(office);
        }

        [HttpGet("simple")]
        public async Task<ActionResult<IEnumerable<object>>> GetOfficesSimple([FromQuery] string? search = null)
        {
            var query = _context.Offices.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(o => o.Name != null && o.Name.Contains(search));
            return Ok(await query
                .OrderBy(o => o.Name)
                .Select(o => new { o.Id, o.Name, o.ReceivingSchedule })
                .ToListAsync());
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
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UpdateOffice] Failed");
                return StatusCode(500, "Failed to update office.");
            }
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
                return BadRequest("Cannot delete office that has users assigned. Please reassign users first.");

            var unitIds = await _context.Units
                .Where(u => u.OfficeId == id)
                .Select(u => u.Id)
                .ToListAsync();
            var hasUsersInUnits = unitIds.Any() && await _context.AppUsers
                .AnyAsync(u => u.UnitId != null && unitIds.Contains(u.UnitId.Value));

            if (hasUsersInUnits)
                return BadRequest("Cannot delete office that has users assigned to its units. Please reassign users first.");

            var hasActiveDocuments = await _context.Documents.AnyAsync(d =>
                d.CurrentOfficeId == id || d.NextOfficeId == id);

            if (hasActiveDocuments)
                return BadRequest("Cannot delete office that has active documents assigned. Please reassign documents first.");

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                await _context.DocumentLogs
                    .Where(dl => dl.OfficeId == id && (dl.OfficeName == null || dl.OfficeName == ""))
                    .ExecuteUpdateAsync(dl => dl.SetProperty(x => x.OfficeName, existing.Name));

                await _context.DocumentLogs
                    .Where(dl => dl.OfficeId == id)
                    .ExecuteUpdateAsync(dl => dl.SetProperty(x => x.OfficeId, (int?)null));

                if (existing.Units != null && existing.Units.Any())
                {
                    foreach (var unit in existing.Units)
                    {
                        await _context.DocumentLogs
                            .Where(dl => dl.UnitId == unit.Id && (dl.UnitName == null || dl.UnitName == ""))
                            .ExecuteUpdateAsync(dl => dl.SetProperty(x => x.UnitName, unit.Name)
                                                      .SetProperty(x => x.OfficeName, existing.Name));

                        await _context.DocumentLogs
                            .Where(dl => dl.UnitId == unit.Id)
                            .ExecuteUpdateAsync(dl => dl.SetProperty(x => x.UnitId, (int?)null));
                    }

                    _context.Units.RemoveRange(existing.Units);
                }

                _context.Offices.Remove(existing);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "[DeleteOffice] Failed");
                return StatusCode(500, "Failed to delete office.");
            }
            return Ok();
        }
    }
}


