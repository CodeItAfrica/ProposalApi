using DocumentFormat.OpenXml.InkML;
using GibsLifesMicroWebApi.Domain;
using GibsLifesMicroWebApi.Domain.YourProject.DTOs;
using GibsLifesMicroWebApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Data.Repositories
{
    public partial class Repository
    {

        //public Task<List<Document>> DocumentSelectAsync(string ownerRefId)
        //{
        //    var query = _db.Documents.Where(x => x.OwnerRefId == ownerRefId);

        //    return query.ToListAsync();
        //}

        //public Task<Document> DocumentSelectThisAsync(long documentId)
        //{
        //    //if (string.IsNullOrWhiteSpace(documentId))
        //    //    throw new ArgumentNullException(nameof(documentId));

        //    return _db.Documents.FirstOrDefaultAsync(x => x.DocumentId == documentId);
        //}

        //public async Task<List<long>> DocumentCreateAsync(string ownerType, string ownerRefId, List<IFormFile> formFiles)
        //{
        //    ownerType = ownerType.ToUpper();
        //    string[] types = { "POLICY", "CLAIM" };

        //    if (!types.Contains(ownerType))
        //        throw new ArgumentOutOfRangeException(nameof(ownerType));

        //    if (formFiles is null)
        //        throw new ArgumentNullException(nameof(formFiles));

        //    if (ownerType == "POLICY")
        //    {
        //        var policy = await PolicySelectThisAsync(ownerRefId);

        //        if (policy is null)
        //            throw new KeyNotFoundException("Policy No you supplied is invalid");
        //    }

        //    if (ownerType == "CLAIM")
        //    {
        //        var claim = await ClaimSelectThisAsync(ownerRefId);

        //        if (claim is null)
        //            throw new KeyNotFoundException("Claim No you supplied is invalid");
        //    }

        //    var documentIds = new List<long>();

        //    foreach (var file in formFiles)
        //    {
        //        if (file == null || file.Length == 0)
        //            throw new Exception("Error in uploaded file, empty content");

        //        var document = new Document
        //        {
        //            OwnerType = ownerType,
        //            OwnerRefId = ownerRefId, 
        //            SubmitDate = DateTime.Now,
        //            DocumentName = file.FileName,
        //            //DocumentId = Guid.NewGuid().ToString(),
        //            Content = file.OpenReadStream().ReadFully(),
        //            //ContentType = "application/json",
        //        };

        //        documentIds.Add(document.DocumentId);
        //        _db.Documents.Add(document);
        //    }

        //    return documentIds;
        //}

        public async Task<IEnumerable<AttachDoc>> GetAllAsync()
        {
            return await _db.AttachDocs
                .Where(d => d.Deleted == 0)
                .OrderByDescending(d => d.AttachID)
                .ToListAsync();
        }

        public async Task<AttachDoc?> GetDocumentByIdAsync(long id)
        {
            return await _db.AttachDocs
                .FirstOrDefaultAsync(d => d.AttachID == id && d.Deleted == 0);
        }

        public async Task<AttachDoc> CreateAsync(AttachDoc attachDoc)
        {
            _db.AttachDocs.Add(attachDoc);
            await _db.SaveChangesAsync();
            return attachDoc;
        }

        public async Task<AttachDoc> UpdateAsync(AttachDoc attachDoc)
        {
            _db.AttachDocs.Update(attachDoc);
            await _db.SaveChangesAsync();
            return attachDoc;
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var attachDoc = await _db.AttachDocs.FindAsync(id);
            if (attachDoc == null)
                return false;

            _db.AttachDocs.Remove(attachDoc);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(long id)
        {
            return await _db.AttachDocs
                .AnyAsync(d => d.AttachID == id && d.Deleted == 0);
        }
        public async Task<IEnumerable<AttachDocResponseDto>> GetAllDocumentsAsync()
        {
            var documents = await  GetAllAsync();
            return documents.Select(MapToResponseDto);
        }

        public async Task<AttachDocResponseDto?> GetServiceDocumentByIdAsync(long id)
        {
            var document = await  GetDocumentByIdAsync(id);
            return document == null ? null : MapToResponseDto(document);
        }

        public async Task<AttachDocResponseDto> CreateDocumentAsync(CreateAttachDocDto createDto)
        {
            byte[]? fileBytes = null;
            string? fileName = null;

            if (createDto.DocumentFile != null && createDto.DocumentFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await createDto.DocumentFile.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();

                // Get the full filename with extension
                fileName = createDto.DocumentFile.FileName;
            }

            var attachDoc = new AttachDoc
            {
                DocNo = createDto.DocNo,
                EntryDate = DateTime.Now,  // CHANGED: Use current date
                DocTypes = createDto.DocTypes,
                DocFrom = createDto.DocFrom,
                DocDetails = fileName,  // CHANGED: Store filename in DocDetails
                zDocuments = fileBytes,
                Active = 1,
                OptionType = createDto.OptionType,
                SubmittedBy = createDto.SubmittedBy ?? "System",
                SubmittedOn = DateTime.UtcNow,
                Deleted = 0
            };

            var createdDoc = await CreateAsync(attachDoc);
            return MapToResponseDto(createdDoc);
        }


        public async Task<AttachDocResponseDto?> UpdateDocumentAsync(long id, UpdateAttachDocDto updateDto)
        {
            var existingDoc = await GetDocumentByIdAsync(id);
            if (existingDoc == null)
                return null;

            // Update fields
            existingDoc.DocNo = updateDto.DocNo;
            existingDoc.EntryDate = updateDto.EntryDate;
            existingDoc.DocTypes = updateDto.DocTypes;
            existingDoc.DocFrom = updateDto.DocFrom;

            // Update file if provided
            if (updateDto.DocumentFile != null && updateDto.DocumentFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await updateDto.DocumentFile.CopyToAsync(memoryStream);
                existingDoc.zDocuments = memoryStream.ToArray();

                // Update DocDetails with new filename
                existingDoc.DocDetails = updateDto.DocumentFile.FileName;
            }
            else
            {
                // Only update DocDetails if no file was uploaded
                existingDoc.DocDetails = updateDto.DocDetails;
            }

            existingDoc.Active = updateDto.Active;
            existingDoc.OptionType = updateDto.OptionType;

            var updatedDoc = await UpdateAsync(existingDoc);
            return MapToResponseDto(updatedDoc);
        }

        // Change return type to IEnumerable to return multiple documents
        public async Task<IEnumerable<AttachDocResponseDto>> GetDocumentByDocNoAsync(string docNo)
        {
            var documents = await _db.AttachDocs
                .Where(d => d.DocNo == docNo && d.Deleted == 0)
                .OrderByDescending(d => d.AttachID)
                .ToListAsync();

            return documents.Select(MapToResponseDto);
        }

        public async Task<bool> DeleteDocumentAsync(long id)
        {
            return await  DeleteAsync(id);
        }

        public async Task<byte[]?> GetDocumentFileAsync(long id)
        {
            var document = await  GetDocumentByIdAsync(id);
            return document?.zDocuments;
        }

        private static AttachDocResponseDto MapToResponseDto(AttachDoc doc)
        {
            return new AttachDocResponseDto
            {
                AttachID = doc.AttachID,
                DocNo = doc.DocNo,
                EntryDate = doc.EntryDate,
                DocTypes = doc.DocTypes,
                DocFrom = doc.DocFrom,
                DocDetails = doc.DocDetails,
                DocumentSize = doc.zDocuments?.Length ?? 0,
                Active = doc.Active,
                OptionType = doc.OptionType,
                SubmittedBy = doc.SubmittedBy,
                SubmittedOn = doc.SubmittedOn
            };
        }
    }
}
