using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BeneficiariesController : ControllerBase
    {
        private readonly BeneficiaryRepository _repository;

        public BeneficiariesController(BeneficiaryRepository repository)
        {
            _repository = repository;
        }

        // GET: api/Beneficiaries?pageNumber=1&pageSize=50
        [HttpGet]
        public async Task<ActionResult> GetBeneficiaries(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            if (pageSize > 100) pageSize = 100; // Limit max page size
            if (pageNumber < 1) pageNumber = 1;

            var (beneficiaries, totalCount) = await _repository.GetAllAsync(pageNumber, pageSize);
            var beneficiaryDtos = beneficiaries.Select(b => MapToReadDto(b));

            return Ok(new
            {
                data = beneficiaryDtos,
                pageNumber,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        // GET: api/Beneficiaries/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BeneficiaryReadDto>> GetBeneficiary(int id)
        {
            var beneficiary = await _repository.GetByIdAsync(id);

            if (beneficiary == null)
            {
                return NotFound(new { message = $"Beneficiary with ID {id} not found" });
            }

            return Ok(MapToReadDto(beneficiary));
        }

        // GET: api/Beneficiaries/policy/POL123
        [HttpGet("policy/{policyNo}")]
        public async Task<ActionResult<IEnumerable<BeneficiaryReadDto>>> GetBeneficiariesByPolicy(string policyNo)
        {
            var beneficiaries = await _repository.GetByPolicyNoAsync(policyNo);
            var beneficiaryDtos = beneficiaries.Select(b => MapToReadDto(b));
            return Ok(beneficiaryDtos);
        }

        // GET: api/Beneficiaries/proposal/PROP123
        [HttpGet("proposal/{proposalNo}")]
        public async Task<ActionResult<IEnumerable<BeneficiaryReadDto>>> GetBeneficiariesByProposal(string proposalNo)
        {
            var beneficiaries = await _repository.GetByProposalNoAsync(proposalNo);
            var beneficiaryDtos = beneficiaries.Select(b => MapToReadDto(b));
            return Ok(beneficiaryDtos);
        }

        // POST: api/Beneficiaries
        [HttpPost]
        public async Task<ActionResult<BeneficiaryReadDto>> CreateBeneficiary([FromForm] BeneficiaryCreateDto dto)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(dto.Surname) ||
                string.IsNullOrWhiteSpace(dto.OtherNames) ||
                string.IsNullOrWhiteSpace(dto.PolicyNo) ||
                string.IsNullOrWhiteSpace(dto.Address) ||
                string.IsNullOrWhiteSpace(dto.Age) ||
                string.IsNullOrWhiteSpace(dto.Sex) ||
                string.IsNullOrWhiteSpace(dto.Relation) ||
                string.IsNullOrWhiteSpace(dto.Type) ||
                string.IsNullOrWhiteSpace(dto.TransFlag))
            {
                return BadRequest(new { message = "All required fields must be provided" });
            }

            // Validate DOB is within valid SQL datetime range (1753-9999)
            if (dto.DOB.Year < 1753 || dto.DOB.Year > 9999)
            {
                return BadRequest(new { message = "Date of Birth must be between 1753 and 9999" });
            }

            try
            {
                var beneficiary = await MapToEntity(dto);
                beneficiary.SubmittedBy = User.Identity?.Name ?? "System";

                var created = await _repository.CreateAsync(beneficiary);
                var readDto = MapToReadDto(created);

                return CreatedAtAction(nameof(GetBeneficiary),
                    new { id = created.TransID }, readDto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating beneficiary", error = ex.Message, innerError = ex.InnerException?.Message });
            }
        }

        // PUT: api/Beneficiaries/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBeneficiary(int id, [FromForm] BeneficiaryUpdateDto dto)
        {
            var existing = await _repository.GetByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = $"Beneficiary with ID {id} not found" });
            }

            try
            {
                await UpdateEntityFromDto(existing, dto);
                existing.ModifiedBy = User.Identity?.Name ?? "System";

                await _repository.UpdateAsync(existing);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating beneficiary", error = ex.Message });
            }
        }

        // DELETE: api/Beneficiaries/5 (Soft delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBeneficiary(int id)
        {
            var success = await _repository.DeleteAsync(id);
            if (!success)
            {
                return NotFound(new { message = $"Beneficiary with ID {id} not found" });
            }

            return NoContent();
        }

        // DELETE: api/Beneficiaries/5/permanent (Hard delete)
        [HttpDelete("{id}/permanent")]
        public async Task<IActionResult> PermanentDeleteBeneficiary(int id)
        {
            var success = await _repository.PermanentDeleteAsync(id);
            if (!success)
            {
                return NotFound(new { message = $"Beneficiary with ID {id} not found" });
            }

            return NoContent();
        }

        private BeneficiaryReadDto MapToReadDto(Beneficiary entity)
        {
            return new BeneficiaryReadDto
            {
                TransID = entity.TransID,
                ProposalNo = entity.ProposalNo,
                Surname = entity.Surname,
                OtherNames = entity.OtherNames,
                Age = entity.Age,
                Sex = entity.Sex,
                DOB = entity.DOB,
                Relation = entity.Relation,
                Percentage = entity.Percentage,
                Type = entity.Type,
                TransFlag = entity.TransFlag,
                PhotoBase64 = entity.Photo != null ? Convert.ToBase64String(entity.Photo) : null,
                HasPhoto = entity.Photo != null && entity.Photo.Length > 0,
                PolicyNo = entity.PolicyNo,
                Address = entity.Address,
                Remark = entity.Remark,
                Tag = entity.Tag,
                Deleted = entity.Deleted,
                Active = entity.Active,
                SubmittedBy = entity.SubmittedBy,
                SubmittedOn = entity.SubmittedOn,
                ModifiedBy = entity.ModifiedBy,
                ModifiedOn = entity.ModifiedOn
            };
        }

        private async Task<Beneficiary> MapToEntity(BeneficiaryCreateDto dto)
        {
            byte[] photoBytes = null;

            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    await dto.Photo.CopyToAsync(memoryStream);
                    photoBytes = memoryStream.ToArray();
                }
            }
            else if (!string.IsNullOrEmpty(dto.PhotoBase64))
            {
                photoBytes = Convert.FromBase64String(dto.PhotoBase64);
            }

            return new Beneficiary
            {
                ProposalNo = dto.ProposalNo,
                Surname = dto.Surname,
                OtherNames = dto.OtherNames,
                Age = dto.Age,
                Sex = dto.Sex,
                DOB = dto.DOB,
                Relation = dto.Relation,
                Percentage = dto.Percentage,
                Type = dto.Type,
                TransFlag = dto.TransFlag,
                Photo = photoBytes,
                PolicyNo = dto.PolicyNo,
                Address = dto.Address,
                Remark = dto.Remark,
                Tag = dto.Tag,
                Deleted = 0,
                Active = 1
            };
        }

        private async Task UpdateEntityFromDto(Beneficiary entity, BeneficiaryUpdateDto dto)
        {
            entity.ProposalNo = dto.ProposalNo;
            entity.Surname = dto.Surname;
            entity.OtherNames = dto.OtherNames;
            entity.Age = dto.Age;
            entity.Sex = dto.Sex;
            entity.DOB = dto.DOB;
            entity.Relation = dto.Relation;
            entity.Percentage = dto.Percentage;
            entity.Type = dto.Type;
            entity.TransFlag = dto.TransFlag;

            if (dto.Photo != null && dto.Photo.Length > 0)
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    await dto.Photo.CopyToAsync(memoryStream);
                    entity.Photo = memoryStream.ToArray();
                }
            }
            else if (!string.IsNullOrEmpty(dto.PhotoBase64))
            {
                entity.Photo = Convert.FromBase64String(dto.PhotoBase64);
            }

            entity.PolicyNo = dto.PolicyNo;
            entity.Address = dto.Address;
            entity.Remark = dto.Remark;
            entity.Tag = dto.Tag;
            entity.Active = dto.Active;
        }
    }
}