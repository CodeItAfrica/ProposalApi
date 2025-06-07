using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using GibsLifesMicroWebApi.Data.Repositories;
using GibsLifesMicroWebApi.Data;

namespace GibsLifesMicroWebApi.Controllers
{
    [Authorize(Roles = "APP,AGENT,CUST")]
    public class DocumentsController : SecureControllerBase
    {
        public DocumentsController(Repository repository, AuthContext authContext) : base(repository, authContext)
        {
        }

        /// <summary>
        /// Fetch a collection of Documents for a Policy/Claim
        /// Please specify either the owner policyNo or claimNo, but not both.
        /// </summary>
        /// <param name="policyNo"></param>
        /// <param name="claimNo"></param>
        /// <returns>A collection of Documents</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FileContentResult>>> 
        ListDocuments([FromQuery]string policyNo, [FromQuery] string claimNo)
        {
            try
            {
                var ownerRefId = string.IsNullOrEmpty(policyNo) ? claimNo : policyNo;
                var documents = await _repository.DocumentSelectAsync(ownerRefId);
                return documents.Select(x => new FileContentResult(x.Content, "application/octet-stream")).ToList();
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Fetch a single Document
        /// </summary>
        /// <param name="documentId"></param>
        /// <returns>The Document with the DocumentId supplied</returns>
        [HttpGet("Search")]
        public async Task<ActionResult> GetDocument([FromQuery] long documentId)
        {
            try
            {
                var document = await _repository.DocumentSelectThisAsync(documentId);

                if (document is null)
                    return NotFound();

                return new FileContentResult(document.Content, "application/octet-stream");
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }

        /// <summary>
        /// Upload Documents and associate with a Policy
        /// </summary>
        /// <param name="policyNo"></param>
        /// <param name="file"></param>
        /// <returns>The newly created Documents IDs</returns>
        [HttpPost("Upload/Policy")]
        public async Task<ActionResult<IEnumerable<string>>> 
            UploadDocumentsForPolicy([FromForm] string policyNo, [FromForm] List<IFormFile> file)
        {
            try
            {
                var documentIds = await _repository.DocumentCreateAsync("POLICY", policyNo, file);
                return Ok(documentIds);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }


        /// <summary>
        /// Upload Documents and associate with a Claim
        /// </summary>
        /// <param name="claimNo"></param>
        /// <param name="file"></param>
        /// <returns>The newly created Documents IDs</returns>
        [HttpPost("Upload/Claim")]
        public async Task<ActionResult<IEnumerable<string>>>
            UploadDocumentsForClaims([FromForm] string claimNo, [FromForm] List<IFormFile> file)
        {
            try
            {
                var documentIds = await _repository.DocumentCreateAsync("CLAIM", claimNo, file);
                return Ok(documentIds);
            }
            catch (Exception ex)
            {
                return ExceptionResult(ex);
            }
        }
    }
}
