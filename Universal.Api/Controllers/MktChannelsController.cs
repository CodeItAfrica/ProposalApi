using Gibs.Domain.Entities;
using Gibs.Infrastructure;
using GibsLifesMicroWebApi.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gibs.Cornerstone.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MktChannelsController : ControllerBase
    {
        private readonly DataContext _context;

        public MktChannelsController(DataContext context)
        {
            _context = context;
        }

        // GET: api/SalesChannels
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SalesChannelDto>>> GetSalesChannels()
        {
            var channels = await _context.SalesChannels
                .Include(c => c.SubChannels)
                .Include(c => c.Branch)
                .ToListAsync();

            var channelDtos = channels.Select(c => MapToDto(c));
            return Ok(channelDtos);
        }

        // GET: api/SalesChannels/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<SalesChannelDto>> GetSalesChannel(string id)
        {
            var channel = await _context.SalesChannels
                .Include(c => c.SubChannels)
                .Include(c => c.Branch)
                .FirstOrDefaultAsync(c => c.ChannelID == id);

            if (channel == null)
            {
                return NotFound();
            }

            return Ok(MapToDto(channel));
        }

        // POST: api/SalesChannels
        [HttpPost]
        public async Task<ActionResult<SalesChannelDto>> CreateSalesChannel(CreateSalesChannelDto createDto)
        {
            // Validate the DTO
            if (string.IsNullOrWhiteSpace(createDto.ChannelID))
            {
                return BadRequest("ChannelID is required.");
            }

            if (string.IsNullOrWhiteSpace(createDto.ChannelName))
            {
                return BadRequest("ChannelName is required.");
            }

            // Check if channel already exists
            if (await _context.SalesChannels.AnyAsync(c => c.ChannelID == createDto.ChannelID))
            {
                return Conflict($"SalesChannel with ID '{createDto.ChannelID}' already exists.");
            }

            // Create new SalesChannel using constructor
            var channel = new SalesChannel(createDto.ChannelID, createDto.ChannelName, createDto.BranchID);

            _context.SalesChannels.Add(channel);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSalesChannel), new { id = channel.ChannelID }, MapToDto(channel));
        }

        // PUT: api/SalesChannels/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateSalesChannel(string id, UpdateSalesChannelDto updateDto)
        {
            if (string.IsNullOrWhiteSpace(updateDto.ChannelName))
            {
                return BadRequest("ChannelName is required.");
            }

            var channel = await _context.SalesChannels.FindAsync(id);
            if (channel == null)
            {
                return NotFound();
            }

            // Use the entity's update methods
            channel.UpdateChannelName(updateDto.ChannelName);
            channel.UpdateBranchID(updateDto.BranchID);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SalesChannelExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }


        // DELETE: api/SalesChannels/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSalesChannel(string id)
        {
            var channel = await _context.SalesChannels.FindAsync(id);
            if (channel == null)
            {
                return NotFound();
            }

            _context.SalesChannels.Remove(channel);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SalesChannelExists(string id)
        {
            return _context.SalesChannels.Any(e => e.ChannelID == id);
        }

        private static SalesChannelDto MapToDto(SalesChannel channel)
        {
            return new SalesChannelDto
            {
                ChannelID = channel.ChannelID,
                ChannelName = channel.ChannelName,
                BranchID = channel.BranchID,
                BranchName = channel.Branch?.Description, // Assuming LocationBranch has BranchName
                SubChannels = channel.SubChannels?.Select(sc => new SalesSubChannelDto
                {
                    ChannelID = sc.ChannelID,
                    SubChannelID = sc.SubChannelID,
                    SubChannelName = sc.SubChannelName,
                    Description = sc.Description
                }).ToList()
            };
        }

        // Helper method to create SalesChannel instance
        // This is a temporary solution - you should add proper constructors or factory methods
        private static SalesChannel CreateSalesChannelInstance(CreateSalesChannelDto createDto)
        {
            // This would need to be implemented based on your domain design
            // Option 1: Add constructor to SalesChannel
            // Option 2: Use a factory method
            // Option 3: Use reflection (not recommended)

            throw new NotImplementedException("You need to implement proper SalesChannel creation. Consider adding a constructor or factory method to the SalesChannel entity.");
        }
    }

    // DTOs
    public class SalesChannelDto
    {
        public string? BranchID { get; set; }
        public string ChannelID { get; set; }
        public string ChannelName { get; set; }
        public string? BranchName { get; set; }
        public List<SalesSubChannelDto>? SubChannels { get; set; }
    }

    public class SalesSubChannelDto
    {
        public string ChannelID { get; set; }
        public string SubChannelID { get; set; }
        public string SubChannelName { get; set; }
        public string Description { get; set; }
    }

    public class CreateSalesChannelDto
    {
        public string ChannelID { get; set; }
        public string ChannelName { get; set; }
        public string? BranchID { get; set; }
    }

    public class UpdateSalesChannelDto
    {
        public string ChannelName { get; set; }
        public string? BranchID { get; set; }
    }
}