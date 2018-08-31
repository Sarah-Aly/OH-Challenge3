using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Configuration;

namespace bfyoc_api
{
    public static class Ratings
    {
        private static HttpClient userClient = new HttpClient { BaseAddress = new Uri("http://serverlessohuser.trafficmanager.net/api/") };
        private static HttpClient productClient = new HttpClient { BaseAddress = new Uri("http://serverlessohproduct.trafficmanager.net/api/") };
        private static readonly string cosmosConnection = ConfigurationManager.AppSettings["CosmosConnection"];

        [FunctionName("CreateRating")]
        public static async Task<IActionResult> CreateRating(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ratings")] HttpRequest req,
            [CosmosDB("RatingsDB","ratings",ConnectionStringSetting = "CosmosConnection")] IAsyncCollector<RatingModel> outputRatings,
            //[CosmosDB("bfyoc", "ratings", ConnectionStringSetting = "CosmosConnection")] IAsyncCollector<RatingModel> outputRatings,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var rating = JsonConvert.DeserializeObject<RatingModel>(await req.ReadAsStringAsync());

            if (string.IsNullOrEmpty(rating.UserId))
            {
                return new BadRequestObjectResult("Specify a user Id");
            }

            var userResponse = await userClient.GetAsync($"GetUser?userId={rating.UserId}");
            if (!userResponse.IsSuccessStatusCode)
            {
                return new BadRequestObjectResult("Unrecognized user");
            }

            var productResponse = await productClient.GetAsync($"GetProduct?productId={rating.ProductId}");
            if (!userResponse.IsSuccessStatusCode)
            {
                return new BadRequestObjectResult("Unrecognized product");
            }

            if (rating.Rating < 0 || rating.Rating > 5)
            {
                return new BadRequestObjectResult("Rating must be between 0 and 5");
            }

            rating.Id = Guid.NewGuid().ToString();
            rating.Timestamp = DateTime.UtcNow;

            await outputRatings.AddAsync(rating);

            return new OkObjectResult(rating);

        }

        [FunctionName("GetRating")]
        public static IActionResult GetRating(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ratings/{ratingId}")] HttpRequest req,
            [CosmosDB("RatingsDB", "ratings", ConnectionStringSetting = "CosmosConnection", Id = "{ratingId}")] RatingModel rating,
            ILogger log)
        {
            if (rating == null)
            {
                return new NotFoundResult();
            }
            else
            {
                return new OkObjectResult(rating);
            }
        }

        [FunctionName("GetRatings")]
        public static IActionResult GetRatings(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ratings/byUser/{userId}")] HttpRequest req,
            [CosmosDB("RatingsDB", "ratings", ConnectionStringSetting = "CosmosConnection", SqlQuery = "SELECT * FROM ratings r WHERE r.UserId = {userId}")] IEnumerable<RatingModel> ratings,
            ILogger log)
        {
            return new OkObjectResult(ratings);
        }
    }

    public class RatingModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public DateTime? Timestamp { get; set; }
        public string UserId { get; set; }
        public string ProductId { get; set; }
        public string LocationName { get; set; }
        public int Rating { get; set; }
        public string UserNotes { get; set; }
    }
}
