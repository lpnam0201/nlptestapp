using Microsoft.AspNetCore.Mvc;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using System.Text;

namespace nlp_test_api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WeatherForecastController> _logger;
    private readonly IConfiguration _configuration;

    public WeatherForecastController(ILogger<WeatherForecastController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }

    [HttpGet("GenerateSasUsingUserDelegation")]
    public async IAsyncEnumerable<string> GenerateSasUsingUserDelegation()
    {
        string endpoint = $"https://nlpteststorage20240415.blob.core.windows.net";
        var blobServiceClient = new BlobServiceClient(
            new Uri(endpoint),
            new DefaultAzureCredential());

        var userDelegationKey = (await blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1))).Value;

        var blobContainerClient = blobServiceClient.GetBlobContainerClient("users");
        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = blobContainerClient.Name,
            Resource = "c",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.List);
        var uriBuilder = new BlobUriBuilder(blobContainerClient.Uri)
        {
            Sas = sasBuilder.ToSasQueryParameters(
                userDelegationKey,
                blobContainerClient.GetParentBlobServiceClient().AccountName)
        };
        var uri = uriBuilder.ToUri();
        yield return $"SAS URI: {uri}" + Environment.NewLine;
        yield return "Try uploading..." + Environment.NewLine;

        var blobContainerClientWithSas = new BlobContainerClient(uri);
        var blobName = Guid.NewGuid().ToString();
        var data = Encoding.ASCII.GetBytes("test-data");
        await blobContainerClientWithSas.UploadBlobAsync(blobName, new BinaryData(data));
        yield return "Uploaded blob!";
    }

    [HttpGet("GetKeyVaultSecrets")]
    public async IAsyncEnumerable<string> GetKeyVaultSecrets()
    {
        yield return _configuration.GetValue<string>("Secret1");
        yield return _configuration.GetValue<string>("Secret2");
    }
}
