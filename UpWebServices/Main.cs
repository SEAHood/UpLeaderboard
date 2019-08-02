using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace UpWebServices
{
    public class Leaderboard
    {
        public Leaderboard(IEnumerable<AccountEntity> accounts)
        {
            Scores = accounts
                .OrderByDescending(a => a.PersonalBest)
                .Select(a => new PlayerScore()
                {
                    Name = a.Username,
                    Score = a.PersonalBest,
                    Timestamp = a.PersonalBestTimestamp
                })
                .ToList();
        }

        [JsonProperty("scores")]
        public List<PlayerScore> Scores { get; set; }
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

    public class Account
    {
        public Account(AccountEntity account)
        {
            Username = account.RowKey;
            PersonalBest = account.PersonalBest;
            PersonalBestTimestamp = account.PersonalBestTimestamp;
        }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("pb")]
        public int PersonalBest { get; set; }

        [JsonProperty("pbTime")]
        public DateTime PersonalBestTimestamp { get; set; }
    }

    public static class Main
    {
        [FunctionName("Leaderboard")]
        public static async Task<IActionResult> LeaderboardHandler(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "leaderboard")] HttpRequest req,
            [StorageAccount("AzureWebJobsStorage")] CloudStorageAccount storageAccount,
            ILogger log)
        {
            var repo = new Repo(storageAccount);
            switch (req.Method.ToUpper())
            {
                case "GET":
                    return await DoGetLeaderboard(repo, log);
                case "POST":
                    return await DoPostLeaderboard(req, repo, log);
            }

            return new BadRequestResult();
        }

        private static async Task<IActionResult> DoPostLeaderboard(HttpRequest req, Repo repo, ILogger log)
        {
            try
            {
                string username;
                string password;
                int? personalBest;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                {
                    var reqString = reader.ReadToEnd();
                    var credCheck = GetCredentialsFromRequest(reqString, out username, out password);
                    if (credCheck == false)
                        return new BadRequestResult();

                    var pbCheck = GetPersonalBestFromRequest(reqString, out personalBest);
                    if (pbCheck == false || !personalBest.HasValue)
                        return new BadRequestResult();
                }

                var account = await repo.GetAccount(username);
                if (account == null || !HashHelper.Verify(password, account.Password))
                    return new UnauthorizedResult();

                account.PersonalBest = personalBest.Value;
                account.PersonalBestTimestamp = DateTime.Now;

                await repo.UpdateAccount(account);

                var response = new OkObjectResult(new Account(account));
                response.ContentTypes.Add("application/json");
                return response;
            }
            catch (Exception e)
            {
                log.LogError(e.Message, e);
                return new InternalServerErrorResult();
            }
        }

        private static async Task<IActionResult> DoGetLeaderboard(Repo repo, ILogger log)
        {
            try
            {
                var accounts = await repo.GetAccounts();
                var response = new OkObjectResult(new Leaderboard(accounts));
                response.ContentTypes.Add("application/json");
                return response;
            }
            catch (Exception e)
            {
                log.LogError(e.Message, e);
                return new InternalServerErrorResult();
            }
        }


        [FunctionName("Signup")]
        public static async Task<IActionResult> SignupHandler(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "signup")] HttpRequest req,
            [StorageAccount("AzureWebJobsStorage")] CloudStorageAccount storageAccount,
            ILogger log)
        {
            return await DoSignup(req, storageAccount, log);
        }


        [FunctionName("Login")]
        public static async Task<IActionResult> LoginHandler(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "login")] HttpRequest req,
            [StorageAccount("AzureWebJobsStorage")] CloudStorageAccount storageAccount,
            ILogger log)
        {
            return await DoLogin(req, storageAccount, log);
        }

        private static async Task<IActionResult> DoLeaderboard(HttpRequest req, CloudStorageAccount storageAccount, ILogger log)
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

                var response = new OkObjectResult(leaderboardJson);
                response.ContentTypes.Add("application/json");
                return response;
            }
            catch (Exception e)
            {
                log.LogError(e.Message, e);
                return new InternalServerErrorResult();
            }
        }

        private static async Task<IActionResult> DoSignup(HttpRequest req, CloudStorageAccount storageAccount, ILogger log)
        {
            try
            {
                string username;
                string password;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                {
                    var reqString = reader.ReadToEnd();
                    var credCheck = GetCredentialsFromRequest(reqString, out username, out password);
                    if (credCheck == false)
                        return new BadRequestResult();
                }

                var repo = new Repo(storageAccount);
                var account = new AccountEntity(username)
                {
                    Password = HashHelper.Encrypt(password),
                    PersonalBest = 0,
                    PersonalBestTimestamp = DateTime.Now
                };
                await repo.CreateAccount(account);

                var response = new OkObjectResult(new Account(account));
                response.ContentTypes.Add("application/json");
                return response;
            }
            catch (StorageException e)
            {
                if (e.Message == "Conflict")
                {
                    log.LogError(e.Message, e);
                    return new Microsoft.AspNetCore.Mvc.ConflictResult();
                }

                return new InternalServerErrorResult();
            }
            catch (Exception e)
            {
                log.LogError(e.Message, e);
                return new InternalServerErrorResult();
            }
        }

        private static async Task<IActionResult> DoLogin(HttpRequest req, CloudStorageAccount storageAccount, ILogger log)
        {
            try
            {
                string username;
                string password;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                {
                    var reqString = reader.ReadToEnd();
                    var credCheck = GetCredentialsFromRequest(reqString, out username, out password);
                    if (credCheck == false)
                        return new BadRequestResult();
                }

                var repo = new Repo(storageAccount);
                var account = await repo.GetAccount(username);
                if (account == null || !HashHelper.Verify(password, account.Password))
                    return new UnauthorizedResult();

                var response = new OkObjectResult(new Account(account));
                response.ContentTypes.Add("application/json");
                return response;
            }
            catch (Exception e)
            {
                log.LogError(e.Message, e);
                return new InternalServerErrorResult();
            }
        }

        private static bool GetCredentialsFromRequest(string req, out string username, out string password)
        {
            username = null;
            password = null;

            var body = JsonConvert.DeserializeObject<dynamic>(req);
            if (body["username"] == null || body["password"] == null)
                return false;

            username = body["username"];
            password = body["password"];
            return true;
        }
        
        private static bool GetPersonalBestFromRequest(string req, out int? personalBest)
        {
            personalBest = null;
            var body = JsonConvert.DeserializeObject<dynamic>(req);

            if (body["personalBest"] == null)
                return false;

            personalBest = body["personalBest"];
            return true;
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
                //board = new Leaderboard();
                log.LogError(e.Message);
                throw;
            }

            var newScore = new PlayerScore { Name = name, Score = score, Timestamp = DateTime.UtcNow };
            board.Scores.Add(newScore);
            await leaderboard.UploadTextAsync(JsonConvert.SerializeObject(board));

            log.LogInformation($"Score logged: {JsonConvert.SerializeObject(newScore)}");
            return JsonConvert.SerializeObject(board);
        }
    }
}
