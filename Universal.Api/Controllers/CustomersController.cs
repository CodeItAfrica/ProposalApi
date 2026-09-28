using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    // UNCHANGED in this pass. Nothing here needed to change for corporate
    // support: [FromBody] model binding picks up IsOrg/OrgName/OrgRegNumber/
    // OrgRegDate automatically since they're just new properties on
    // CreateNewCustomerRequest/CreateNewCustomerRequestMaster, and
    // ModelState.IsValid automatically invokes each DTO's Validate()
    // (IValidatableObject) - no controller-level wiring required for either.
    [Route("api/v1/[controller]")]
    [ApiController]
    public class CustomersController : SecureControllerBase
    {
        private readonly Settings _settings;
        private readonly InsuredRepository _insuredrepository;
        private readonly Repository _repository;
        private readonly AuthContext _authContext;

        public CustomersController(
            InsuredRepository insuredrepository,
            Repository repository,
            AuthContext authContext,
            Settings settings)
            : base(repository, authContext)
        {
            _settings = settings;
            _insuredrepository = insuredrepository;
            _repository = repository;
            _authContext = authContext;
        }

        // GET api/v1/Customers?pageNumber=1&pageSize=10
        [HttpGet]
        public async Task<IActionResult> GetAllCustomers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (pageNumber < 1)
                    return BadRequest(new { message = "Page number must be greater than 0." });

                if (pageSize < 1 || pageSize > 100)
                    return BadRequest(new { message = "Page size must be between 1 and 100." });

                var result = await _insuredrepository.GetAllCustomersAsync(pageNumber, pageSize);

                if (result.Data == null || !result.Data.Any())
                    return NotFound(new { message = "No customers found." });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // GET api/v1/Customers/search?filter...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<CustomerResult>>> ListCustomers([FromQuery] FilterPaging filter)
        {
            try
            {
                var customers = await _insuredrepository.CustomerSelectAsync(filter);
                return Ok(customers.Select(x => new CustomerResult(x)).ToList());
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // GET api/v1/Customers/{customerId}
        [HttpGet("{customerId}")]
        public async Task<ActionResult> GetCustomer(string customerId)
        {
            try
            {
                var customer = await _insuredrepository.CustomerSelectThisAsync(customerId);

                if (customer == null)
                    return NotFound();

                return Ok(customer);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // POST api/v1/Customers
        [HttpPost]
        public async Task<ActionResult<CustomerResult>> CreateCustomer([FromBody] CreateNewCustomerRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return ValidationProblem(ModelState);

                var customer = await _insuredrepository.CustomerCreate(request);
                await _repository.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetCustomer),
                    new { customerId = customer.InsuredID },
                    new CustomerResult(customer));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // PATCH api/v1/Customers/{customerId}
        [HttpPatch("{customerId}")]
        public async Task<ActionResult<CustomerResult>> UpdateCustomer(string customerId, [FromBody] UpdateCustomerRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return ValidationProblem(ModelState);

                var customer = await _insuredrepository.CustomerSelectThisAsync(customerId);
                if (customer == null)
                    return NotFound();

                _insuredrepository.ApplyUpdate(customer, request);
                await _repository.SaveChangesAsync();

                return Ok(new CustomerResult(customer));
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // DELETE api/v1/Customers/{customerId}
        [HttpDelete("{customerId}")]
        public async Task<IActionResult> DeactivateCustomer(string customerId)
        {
            try
            {
                var customer = await _insuredrepository.CustomerSelectThisAsync(customerId);
                if (customer == null)
                    return NotFound();

                customer.Deleted = 1;
                customer.Active = 0;
                customer.ModifiedOn = DateTime.Now;

                await _repository.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // GET api/v1/Customers/by-quotation/{quotationNumber}
        // NOTE (pre-existing): operates on group-policy members
        // (MemberDetailsDto / GetGrpMembersByPolicyAsync), a different
        // resource from the rest of this controller, and the route shape of
        // Add below (api/v1/Customers/{quotationNumber}, a bare {value}
        // segment on the controller root) can collide with GetCustomer's
        // api/v1/Customers/{customerId} for any value that happens to also
        // be a valid quotation number.
        [HttpGet("by-quotation/{quotationNumber}")]
        public async Task<IActionResult> GetByQuotation(string quotationNumber)
        {
            try
            {
                var members = await _repository.GetGrpMembersByPolicyAsync(quotationNumber);
                return Ok(members);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        // POST api/v1/Customers/{quotationNumber}
        [HttpPost("{quotationNumber}")]
        public async Task<IActionResult> Add(string quotationNumber, [FromBody] MemberDetailsDto dto)
        {
            try
            {
                var user = GetCurrentUser();
                var result = await _repository.AddMemberAndRecalculateAsync(quotationNumber, dto, user);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        private string GetCurrentUser()
        {
            return _authContext?.User?.AppId ?? "SYSTEM";
        }
    }
}