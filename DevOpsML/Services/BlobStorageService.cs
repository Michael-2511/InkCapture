using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;


namespace DevOpsML.Services
{
    public class BlobStorageService
    {
        private readonly BlobContainerClient _containerClient;

        public BlobStorageService(IConfiguration configuration)
        {
            var connectionString = configuration["AzureBlobStorage:ConnectionString"];
            var containerName = configuration["AzureBlobStorage:ContainerName"];

            // Create the BlobServiceClient and get the container client
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Create container if it doesn't exist
            _containerClient.CreateIfNotExists();
        }

        // Upload a file to Blob Storage and store metadata (recognized text)
        public async Task<string> UploadBlobAsync(string filePath, string recognizedText)
        {
            //var fileName = Path.GetFileName(filePath);
            //var blobClient = _containerClient.GetBlobClient(fileName);

            // Get the original file name and extension
            var originalFileName = Path.GetFileName(filePath);
            var fileExtension = Path.GetExtension(originalFileName);

            // Generate a new name with the original extension
            var fileName = $"{Path.GetFileNameWithoutExtension(Path.GetRandomFileName())}{fileExtension}";

            var blobClient = _containerClient.GetBlobClient(fileName);

            // Log the file extension for debugging
            Console.WriteLine($"File Extension: {fileExtension}");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            if (!allowedExtensions.Contains(fileExtension.ToLower()))
            {
                throw new InvalidOperationException($"Invalid file type. Only image files are allowed. File Extension: {fileExtension}");
            }


            // Upload the file
            await blobClient.UploadAsync(filePath, overwrite: true);

            // Set metadata (recognized text)
            var metadata = new Dictionary<string, string>
            {
                { "RecognizedText", SanitizeText(recognizedText.ToLower()) },
                { "UploadedAt", DateTime.UtcNow.ToString("o") }
            };
            await blobClient.SetMetadataAsync(metadata);

            return blobClient.Uri.ToString(); // Return the blob URL
        }

        // Helper method to remove invalid characters from text
        private string SanitizeText(string text)
        {
            // Ensure that the text contains only allowed characters
            var allowedChars = text.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray();
            return new string(allowedChars);
        }

        // List all blobs in the container with metadata
        public async Task<List<(string FileName, string RecognizedText, string UploadedAt, string BlobUrl)>> ListBlobsAsync()
        {
            var blobs = new List<(string, string, string, string)>();

            // Iterate over each blob in the container
            await foreach (var blobItem in _containerClient.GetBlobsAsync())
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var properties = await blobClient.GetPropertiesAsync();

                // Get metadata values
                var recognizedText = properties.Value.Metadata.TryGetValue("RecognizedText", out var text) ? text : "N/A";
                var uploadedAt = properties.Value.Metadata.TryGetValue("UploadedAt", out var timestamp) ? timestamp : "Unknown";
                var blobUrl = blobClient.Uri.ToString();

                // Add blob details to the list
                blobs.Add((blobItem.Name, recognizedText, uploadedAt, blobUrl));
            }
            return blobs;
        }
    }
}
