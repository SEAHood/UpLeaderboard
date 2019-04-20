using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace UpLeaderboard
{
    public class Leaderboard
    {
        [JsonProperty("scores")]
        public List<PlayerScore> Scores { get; set; } = new List<PlayerScore>();
    }

    public class PlayerScore
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("score")]
        public int Score { get; set; }
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    public static class Main
    {
        [FunctionName("Leaderboard")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [StorageAccount("AzureWebJobsStorage")] CloudStorageAccount storageAccount,
            ILogger log)
        {
            try
            {
                var client = storageAccount.CreateCloudBlobClient();
                var container = client.GetContainerReference("leaderboards");
                await container.CreateIfNotExistsAsync();

                var leaderboardBlob = container.GetBlockBlobReference("leaderboard.json");
                var leaderboardJson = "";

                switch (req.Method.ToLower())
                {
                    case "get":
                        leaderboardJson = await leaderboardBlob.DownloadTextAsync();
                        break;
                    case "post":
                        var reqBody = await req.ReadAsStringAsync();
                        log.LogInformation($"Received POST payload: {reqBody}");

                        dynamic data = JsonConvert.DeserializeObject(reqBody);
                        var name = (string)data.name;
                        var score = (string)data.score;
                        if (name == null || score == null || name == "" || !int.TryParse(score, out var scoreInt)) 
                            return new BadRequestResult();
                        leaderboardJson = await PostScore(leaderboardBlob, name, scoreInt, log);
                        break;
                }

                return new OkObjectResult(leaderboardJson);
            }
            catch (Exception e)
            {
                log.LogError(e.Message, e);
                return new InternalServerErrorResult();
            }
        }

        private static async Task<string> PostScore(CloudBlockBlob leaderboard, string name, int score, ILogger log)
        {
            Leaderboard board;
            try
            {
                board = JsonConvert.DeserializeObject<Leaderboard>(await leaderboard.DownloadTextAsync());

                if (board.Scores == null)
                    board.Scores = new List<PlayerScore>();
            }
            catch (Exception e) // File is probably corrupted somehow
            {
                board = new Leaderboard();
                log.LogError(e.Message);
            }

            var newScore = new PlayerScore { Name = name, Score = score, Timestamp = DateTime.UtcNow };
            board.Scores.Add(newScore);
            await leaderboard.UploadTextAsync(JsonConvert.SerializeObject(board));

            log.LogInformation($"Score logged: {JsonConvert.SerializeObject(newScore)}");
            return JsonConvert.SerializeObject(board);
        }
    }
}
