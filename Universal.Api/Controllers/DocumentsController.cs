using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Domain.YourProject.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    public class DocumentsController : SecureControllerBase
    {
        public DocumentsController(Repository repository, AuthContext authContext) : base(repository, authContext)
        {
        }

        ///// <summary>
        ///// Fetch a collection of Documents for a Policy/Claim
        ///// Please specify either the owner policyNo or claimNo, but not both.
        ///// </summary>
        ///// <param name="policyNo"></param>
        ///// <param name="claimNo"></param>
        ///// <returns>A collection of Documents</returns>
        //[HttpGet]
        //public async Task<ActionResult<IEnumerable<FileContentResult>>> 
        //ListDocuments([FromQuery]string policyNo, [FromQuery] string claimNo)
        //{
        //    try
        //    {
        //        var ownerRefId = string.IsNullOrEmpty(policyNo) ? claimNo : policyNo;
        //        var documents = await _repository.DocumentSelectAsync(ownerRefId);
        //        return documents.Select(x => new FileContentResult(x.Content, "application/octet-stream")).ToList();
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        ///// <summary>
        ///// Fetch a single Document
        ///// </summary>
        ///// <param name="documentId"></param>
        ///// <returns>The Document with the DocumentId supplied</returns>
        //[HttpGet("Search")]
        //public async Task<ActionResult> GetDocument([FromQuery] long documentId)
        //{
        //    try
        //    {
        //        var document = await _repository.DocumentSelectThisAsync(documentId);

        //        if (document is null)
        //            return NotFound();

        //        return new FileContentResult(document.Content, "application/octet-stream");
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        ///// <summary>
        ///// Upload Documents and associate with a Policy
        ///// </summary>
        ///// <param name="policyNo"></param>
        ///// <param name="file"></param>
        ///// <returns>The newly created Documents IDs</returns>
        //[HttpPost("Upload/Policy")]
        //public async Task<ActionResult<IEnumerable<string>>> 
        //    UploadDocumentsForPolicy([FromForm] string policyNo, [FromForm] List<IFormFile> file)
        //{
        //    try
        //    {
        //        var documentIds = await _repository.DocumentCreateAsync("POLICY", policyNo, file);
        //        return Ok(documentIds);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}


        ///// <summary>
        ///// Upload Documents and associate with a Claim
        ///// </summary>
        ///// <param name="claimNo"></param>
        ///// <param name="file"></param>
        ///// <returns>The newly created Documents IDs</returns>
        //[HttpPost("Upload/Claim")]
        //public async Task<ActionResult<IEnumerable<string>>>
        //    UploadDocumentsForClaims([FromForm] string claimNo, [FromForm] List<IFormFile> file)
        //{
        //    try
        //    {
        //        var documentIds = await _repository.DocumentCreateAsync("CLAIM", claimNo, file);
        //        return Ok(documentIds);
        //    }
        //    catch (Exception ex)
        //    {
        //        return ExceptionResult(ex);
        //    }
        //}

        // GET: api/AttachDocs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<AttachDocResponseDto>>> GetAttachDocs()
        {
            var documents = await _repository.GetAllDocumentsAsync();
            return Ok(documents);
        }

        // GET: api/AttachDocs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<AttachDocResponseDto>> GetAttachDoc(long id)
        {
            var document = await _repository.GetServiceDocumentByIdAsync(id);

            if (document == null)
            {
                return NotFound();
            }

            return document;
        }

        // GET: api/AttachDocs/5/file
        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetDocumentFile(long id)
        {
            var fileBytes = await _repository.GetDocumentFileAsync(id);

            if (fileBytes == null || fileBytes.Length == 0)
            {
                return NotFound("Document file not found");
            }

            var document = await _repository.GetServiceDocumentByIdAsync(id);
            var fileName = $"{document?.DocNo ?? "document"}.bin";

            return File(fileBytes, "application/octet-stream", fileName);
        }

        // POST: api/AttachDocs
        [HttpPost]
        public async Task<ActionResult<AttachDocResponseDto>> PostAttachDoc([FromForm] CreateAttachDocDto createDto)
        {
            try
            {
                var createdDoc = await _repository.CreateDocumentAsync(createDto);
                return CreatedAtAction(nameof(GetAttachDoc), new { id = createdDoc.AttachID }, createdDoc);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error creating document: {ex.Message}");
            }
        }

        // PUT: api/AttachDocs/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAttachDoc(long id, [FromForm] UpdateAttachDocDto updateDto)
        {
            if (id <= 0)
            {
                return BadRequest("Invalid ID");
            }

            var updatedDoc = await _repository.UpdateDocumentAsync(id, updateDto);

            if (updatedDoc == null)
            {
                return NotFound();
            }

            return Ok(updatedDoc);
        }
        [HttpGet("docno")]
        public async Task<ActionResult<IEnumerable<AttachDocResponseDto>>> GetAttachDocByDocNo([FromQuery] string docNo)
        {
            if (string.IsNullOrWhiteSpace(docNo))
            {
                return BadRequest("DocNo cannot be empty");
            }

            var documents = await _repository.GetDocumentByDocNoAsync(docNo);

            if (documents == null || !documents.Any())
            {
                return NotFound($"No documents found with DocNo '{docNo}'");
            }

            return Ok(documents);
        }
        // DELETE: api/AttachDocs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAttachDoc(long id)
        {
            var result = await _repository.DeleteDocumentAsync(id);

            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
