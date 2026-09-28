using GibsLifesMicroWebApi.Data;
using GibsLifesMicroWebApi.Data.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace GibsLifesMicroWebApi.Controllers
{
    [Route("api/uploads")]
    [ApiController]
    public class UploadController : SecureControllerBase
    { 
        private readonly string _templateFolder;
        public UploadController(Repository repo, AuthContext ctx) : base(repo, ctx)
        {
            // Assuming your templates are in the "Uploads/Templates" folder
            _templateFolder = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", "Templates");
        }



        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            // move your existing upload logic here
            return Ok();
        }

        [HttpPost("validate")]
        public async Task<IActionResult> Validate(IFormFile file)
        {
            return Ok();
        }


        [HttpGet("template/csv")]
        public IActionResult CsvTemplate()
        {
            var fileName = "ProductTemplate.csv";
            var filePath = Path.Combine(_templateFolder, fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("CSV template not found.");

            // Return the file as a download
            return PhysicalFile(filePath, "text/csv", fileName);
        }

        [HttpGet("template/excel")]
        public IActionResult ExcelTemplate()
        {
            var fileName = "ProductTemplate.xlsx";
            var filePath = Path.Combine(_templateFolder, fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Excel template not found.");

            // Return the file as a download
            return PhysicalFile(filePath,
                                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                fileName);
        }

    }
}
