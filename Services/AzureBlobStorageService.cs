using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SquadInternal.Services
{
    public class AzureBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;

      
            public AzureBlobStorageService(
                BlobServiceClient blobServiceClient,
                IConfiguration configuration)
            {
                var containerName = configuration["AzureStorage:ContainerName"];

                if (string.IsNullOrWhiteSpace(containerName))
                    throw new InvalidOperationException("AzureStorage:ContainerName is missing.");

                _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                _containerClient.CreateIfNotExists(PublicAccessType.None);
            }

            /// <summary>
            /// Uploads a file to Azure Blob Storage under a folder named after the employeeId
            /// and returns the blob's unique name (used to reference/download/delete it later).
            /// </summary>
            public async Task<string> UploadAsync(IFormFile file, int employeeId)
        {
            var safeFileName = Path.GetFileName(file.FileName);
            var blobName = $"{employeeId}/{Guid.NewGuid()}_{safeFileName}";

            var blobClient = _containerClient.GetBlobClient(blobName);

            await using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders
            {
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
            });

            return blobName;
        }

        /// <summary>
        /// Opens a read stream for the given blob name, along with its content type.
        /// </summary>
        public async Task<(Stream Content, string ContentType)> DownloadAsync(string blobName)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            var download = await blobClient.DownloadStreamingAsync();
            var contentType = download.Value.Details.ContentType;

            return (download.Value.Content, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        }

        public async Task DeleteAsync(string blobName)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}
