using Azure.AI.Vision;
using Azure;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using DevOpsML.Services;

namespace DevOpsML.Controllers
{
    public class HandwritingController : Controller
    {
        // Pentru Computer Vision
        private readonly string _endpoint = "https://handwriting-recognition-app.cognitiveservices.azure.com/";
        private readonly string _apiKey = "4kFmDkOEgkPkbh1I9P9H6mLQbDa5aLlqyLAq0i0z5CTLWTZxUU92JQQJ99BAACi5YpzXJ3w3AAAFACOGGN1V";

        private readonly BlobStorageService _blobStorageService;

        public HandwritingController(BlobStorageService blobStorageService)
        {
            _blobStorageService = blobStorageService;
        }

        [HttpGet]
        public IActionResult UploadImage()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ViewData["Error"] = "Please upload a valid image.";
                return View();
            }

            // Get the original file extension
            var originalExtension = Path.GetExtension(file.FileName);

            // Validate the file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            if (!allowedExtensions.Contains(originalExtension.ToLowerInvariant()))
            {
                ViewData["Error"] = "Invalid file type. Only image files are allowed.";
                return View();
            }

            // Save the uploaded file to a temporary path with the original extension
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}{originalExtension}");

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Call the Azure Computer Vision API
            var result = await RecognizeHandwriting(tempFilePath);

            // Upload the image and recognized text to Azure Blob Storage
            var blobUrl = await _blobStorageService.UploadBlobAsync(tempFilePath, result);

            // Delete the temp file
            System.IO.File.Delete(tempFilePath);

            // Pass the recognized text and uploaded image URL to the view
            ViewData["Result"] = result;
            ViewData["UploadedImageUrl"] = blobUrl; // Set the image URL for display

            return View();
        }

        private async Task<string> RecognizeHandwriting(string filePath)
        {
            // Create client
            var client = new ComputerVisionClient(
                new ApiKeyServiceClientCredentials(_apiKey)
            )
            {
                Endpoint = _endpoint
            };

            // Open the file stream
            using var stream = System.IO.File.OpenRead(filePath);

            // Analyze the uploaded image
            var readResults = await client.ReadInStreamAsync(stream);
            string operationId = readResults.OperationLocation.Split('/').Last();

            ReadOperationResult results;
            do
            {
                results = await client.GetReadResultAsync(Guid.Parse(operationId));
                await Task.Delay(1000);
            } while (results.Status == OperationStatusCodes.Running || results.Status == OperationStatusCodes.NotStarted);

            if (results.Status == OperationStatusCodes.Succeeded)
            {
                return string.Join("\n", results.AnalyzeResult.ReadResults
                    .SelectMany(r => r.Lines)
                    .Select(l => l.Text));
            }

            return "No readable handwriting detected.";
        }

        [HttpGet]
        public async Task<IActionResult> History()
        {
            var history = await _blobStorageService.ListBlobsAsync();
            return View(history);
        }
    }
}
