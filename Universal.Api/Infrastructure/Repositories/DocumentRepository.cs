using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using GibsLifesMicroWebApi.Models;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {

        public Task<List<Document>> DocumentSelectAsync(string ownerRefId)
        {
            var query = _db.Documents.Where(x => x.OwnerRefId == ownerRefId);

            return query.ToListAsync();
        }

        public Task<Document> DocumentSelectThisAsync(long documentId)
        {
            //if (string.IsNullOrWhiteSpace(documentId))
            //    throw new ArgumentNullException(nameof(documentId));

            return _db.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        }

        public async Task<List<long>> DocumentCreateAsync(string ownerType, string ownerRefId, List<IFormFile> formFiles)
        {
            ownerType = ownerType.ToUpper();
            string[] types = { "POLICY", "CLAIM" };

            if (!types.Contains(ownerType))
                throw new ArgumentOutOfRangeException(nameof(ownerType));

            if (formFiles is null)
                throw new ArgumentNullException(nameof(formFiles));

            if (ownerType == "POLICY")
            {
                var policy = await PolicySelectThisAsync(ownerRefId);

                if (policy is null)
                    throw new KeyNotFoundException("Policy No you supplied is invalid");
            }

            if (ownerType == "CLAIM")
            {
                var claim = await ClaimSelectThisAsync(ownerRefId);

                if (claim is null)
                    throw new KeyNotFoundException("Claim No you supplied is invalid");
            }

            var documentIds = new List<long>();

            foreach (var file in formFiles)
            {
                if (file == null || file.Length == 0)
                    throw new Exception("Error in uploaded file, empty content");

                var document = new Document
                {
                    OwnerType = ownerType,
                    OwnerRefId = ownerRefId, 
                    SubmitDate = DateTime.Now,
                    DocumentName = file.FileName,
                    //DocumentId = Guid.NewGuid().ToString(),
                    Content = file.OpenReadStream().ReadFully(),
                    //ContentType = "application/json",
                };

                documentIds.Add(document.DocumentId);
                _db.Documents.Add(document);
            }

            return documentIds;
        }
    }
}
