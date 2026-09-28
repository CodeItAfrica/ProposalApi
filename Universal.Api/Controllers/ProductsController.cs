using Gibs.Domain.Entities;
using Gibs.Domain.Entities.others;
using GibsLifesMicroWebApi.Contracts.V1;
using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Models;
using GibsLifesMicroWebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    public class ProductsController : SecureControllerBase
    {
        private readonly ProductSettingService _service;
        private readonly SubRiskSectionService _sectionservice;
        private readonly SubRiskSMIService _smiservice;
        public ProductsController(Repository repository,ProductSettingService service, SubRiskSectionService sectionservice, SubRiskSMIService smiservice, AuthContext authContext) : base(repository, authContext)
        {
            _service = service;
            _sectionservice = sectionservice;
            _smiservice = smiservice;
        }


        [HttpPatch("{subRiskId}/Type")]
        public async Task<IActionResult> UpdateA6(string subRiskId, [FromBody] SubRiskA6UpdateDto dto)
        {
            var updated = await _repository.UpdateA6Async(subRiskId, dto);
            if (!updated) return NotFound(new { message = "SubRisk not found" });

            return Ok(new { message = "Type updated successfully" });
        }   
        /// <summary>
        /// Fetch a collection of products.
        /// </summary>
        /// <returns>A collection of products</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductResult>>> ListProducts([FromQuery] FilterPaging filter)
        {
            try
            {
                var subrisks = await _repository.ProductSelectAsync(filter);
                return Ok(subrisks.Select(x => new ProductResult(x)).ToList());
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        ///// <summary>
        ///// Fetch a single product.
        ///// </summary>
        ///// <param name="productId">Id of the product to get.</param>
        ///// <returns>The product with the subrisk Id</returns>
        //[HttpGet("{productId}")]
        //public async Task<ActionResult<ProductResult>> GetProduct(string productId)
        //{
        //    try
        //    {
        //        var product = await _repository.ProductSelectThisAsync(productId);

        //        if (product is null)
        //            return NotFound();

        //        return Ok(new ProductResult(product));
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}


        //[HttpGet("{code}")]
        //public async Task<IActionResult> Get(string code)
        //{
        //    var item = await _repository.GetByCodeAsync(code);
        //    return item == null ? NotFound() : Ok(item);
        //}

        //[HttpPost]
        //public async Task<IActionResult> Create(ProductSettings config)
        //{
        //    await _repository.AddAsync(config);
        //    return Ok(config);
        //}

        //[HttpPut("{id}")]
        //public async Task<IActionResult> Update(string id, ProductSettings model)
        //{
        //    await _repository.UpdateAsync(model);
        //    return NoContent();
        //}

        //[HttpDelete("{id}")]
        //public async Task<IActionResult> Delete(string id)
        //{
        //    await _repository.DeleteAsync(id);
        //    return NoContent();
        //}

        [HttpGet("AllProductSettings")]
        [ProducesResponseType(typeof(PagedResultDto<ProductSettingDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResultDto<ProductSettingDto>>> List(
              [FromQuery] ProductSettingQueryDto query,
              CancellationToken ct)
              => Ok(await _service.ListAsync(query, ct));

        [HttpGet("{productId}")]
        [ProducesResponseType(typeof(ProductSettingDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProductSettingDto>> Get(
            string productId,
            CancellationToken ct)
        {
            var item = await _service.GetAsync(productId, ct);

            if (item == null)
                return NotFound();

            return Ok(item);
        }

        [HttpPost]
        public async Task<IActionResult> Insert(
            [FromBody] ProductSettingDto dto,
            CancellationToken ct)
        {
            var result = await _service.InsertAsync(dto, ct);

            return result ? Ok() : BadRequest();
        }

        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] ProductSettingDto dto,
            CancellationToken ct)
        {
            var result = await _service.UpdateAsync(dto, ct);

            return result ? Ok() : NotFound();
        }

        [HttpDelete("{productId}")]
        public async Task<IActionResult> Delete(
            string productId,
            CancellationToken ct)
        {
            var result = await _service.DeleteAsync(productId, ct);

            return result ? Ok() : NotFound();
        }

        [HttpGet("AllProductSections")]
        [ProducesResponseType(typeof(IEnumerable<SubRiskSectionDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SubRiskSectionDto>>> List(
         [FromQuery] SubRiskSectionQueryDto query,
         CancellationToken ct)
         => Ok(await _sectionservice.ListAsync(query, ct));

        [HttpGet("{sectionId:long}")]
        [ProducesResponseType(typeof(SubRiskSectionDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubRiskSectionDto>> Get(
            long sectionId,
            CancellationToken ct)
        {
            var item = await _sectionservice.GetAsync(sectionId, ct);

            if (item == null)
                return NotFound();

            return Ok(item);
        }

        [HttpPost("ProductNewSection")]
        public async Task<IActionResult> Insert(
            [FromBody] SubRiskSectionDto dto,
            CancellationToken ct)
        {
            var result = await _sectionservice.InsertAsync(dto, ct);

            return result ? Ok() : BadRequest();
        }

        [HttpPut("ProductUpdateSection")]
        public async Task<IActionResult> Update(
            [FromBody] SubRiskSectionDto dto,
            CancellationToken ct)
        {
            var result = await _sectionservice.UpdateAsync(dto, ct);

            return result ? Ok() : NotFound();
        }

        [HttpDelete("{sectionId:long}")]
        public async Task<IActionResult> Delete(
            long sectionId,
            CancellationToken ct)
        {
            var result = await _sectionservice.DeleteAsync(sectionId, ct);

            return result ? Ok() : NotFound();
        }

        [HttpGet("AllProductSmis")]
        [ProducesResponseType(typeof(IEnumerable<SubRiskSMIDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SubRiskSMIDto>>> List(
            [FromQuery] SubRiskSMIQueryDto query,
            CancellationToken ct)
            => Ok(await _smiservice.ListAsync(query, ct));

        [HttpGet("{sid:long}")]
        [ProducesResponseType(typeof(SubRiskSMIDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SubRiskSMIDto>> GetSmi(
            long sid,
            CancellationToken ct)
        {
            var item = await _smiservice.GetAsync(sid, ct);

            if (item == null)
                return NotFound();

            return Ok(item);
        }

        [HttpPost("ProductNewSmi")]
        public async Task<IActionResult> Insert(
            [FromBody] SubRiskSMIDto dto,
            CancellationToken ct)
        {
            var result = await _smiservice.InsertAsync(dto, ct);

            return result ? Ok() : BadRequest();
        }

        [HttpPut("ProductUpdateSmi")]
        public async Task<IActionResult> Update(
            [FromBody] SubRiskSMIDto dto,
            CancellationToken ct)
        {
            var result = await _smiservice.UpdateAsync(dto, ct);

            return result ? Ok() : NotFound();
        }

        [HttpDelete("{sid:long}")]
        public async Task<IActionResult> Deletesmi(
            long sid,
            CancellationToken ct)
        {
            var result = await _smiservice.DeleteAsync(sid, ct);

            return result ? Ok() : NotFound();
        }

    }
}
