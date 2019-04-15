using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    }

    public static class Main
    {
        [FunctionName("Leaderboard")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            [Blob("leaderboards/leaderboard.json", FileAccess.Read)] Stream inBlob,
            [Blob("leaderboards/leaderboard.json", FileAccess.Write)] Stream outBlob,
            ILogger log)
        {
            try
            {
                var leaderboardJson = "";
                switch (req.Method.ToLower())
                {
                    case "get":
                        leaderboardJson = inBlob != null ? GetLeaderboard(inBlob) : JsonConvert.SerializeObject(new Leaderboard());
                        break;
                    case "post":
                        dynamic data = JsonConvert.DeserializeObject(await req.ReadAsStringAsync());
                        var name = (string)data.name;
                        var score = (string)data.score;
                        if (name == null || score == null || name == "" || !int.TryParse(score, out var scoreInt)) 
                            return new BadRequestResult();
                        leaderboardJson = PostScore(inBlob, outBlob, name, scoreInt);
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

        private static string GetLeaderboard(Stream inBlob)
        {
            using (var sw = new StreamReader(inBlob))
            {
                var body = sw.ReadToEnd();
                return body;
            }
        }

        private static string PostScore(Stream inBlob, Stream outBlob, string name, int score)
        {
            Leaderboard board;
            if (inBlob == null)
                board = new Leaderboard();
            else
            {
                using (var sw = new StreamReader(inBlob))
                {
                    var body = sw.ReadToEnd();
                    board = JsonConvert.DeserializeObject<Leaderboard>(body);
                }

                if (board == null)
                    board = new Leaderboard();
            }
            
            board.Scores.Add(new PlayerScore {Name = name, Score = score});

            using (var sw = new StreamWriter(outBlob, Encoding.Unicode))
            {
                sw.Write(JsonConvert.SerializeObject(board));
                sw.Flush();
            }

            return JsonConvert.SerializeObject(board);
        }
    }
}
