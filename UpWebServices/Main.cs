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
using Newtonsoft.Json;

namespace UpWebServices
{
    public class Leaderboard
    {
        public Leaderboard(IEnumerable<AccountEntity> accounts)
        {
            Scores = accounts
                .OrderByDescending(a => a.PersonalBest)
                .Where(a => a.PersonalBest > 0)
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
            PersonalBestToken = account.PersonalBestToken;
        }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("pb")]
        public int PersonalBest { get; set; }

        [JsonProperty("pbTime")]
        public DateTime PersonalBestTimestamp { get; set; }

        [JsonProperty("pbToken")]
        public string PersonalBestToken { get; set; }
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

        private static async Task<IActionResult> DoPostLeaderboard(HttpRequest req, Repo repo, ILogger log)
        {
            try
            {
                string username;
                int? personalBest;
                string personalBestToken;
                using (var reader = new StreamReader(req.Body, Encoding.UTF8))
                {
                    var reqString = reader.ReadToEnd();
                    var userCheck = GetUsernameFromRequest(reqString, out username);
                    if (userCheck == false)
                    {
                        log.LogInformation("PB was null");
                        return new BadRequestResult();
                    }

                    var pbCheck = GetPersonalBestFromRequest(reqString, out personalBest, out personalBestToken);
                    if (pbCheck == false || !personalBest.HasValue)
                    {
                        log.LogInformation("PB was null");
                        return new BadRequestResult();
                    }
                }

                var account = await repo.GetAccount(username);
                if (account == null || personalBestToken != account.PersonalBestToken)
                {
                    log.LogInformation("PB token mismatch");
                    return new UnauthorizedResult();
                }

                account.PersonalBest = personalBest.Value;
                account.PersonalBestTimestamp = DateTime.Now;

                await repo.UpdateAccount(account);

                log.LogInformation($"New PB: {account.Username} - {account.PersonalBest}");

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
                log.LogInformation("Fetching leaderboard");
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

                log.LogInformation($"{account.Username} CREATED AN ACCOUNT");
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
                
                account.PersonalBestToken = Guid.NewGuid().ToString();
                await repo.UpdateAccount(account);

                log.LogInformation($"{account.Username} LOGGED IN");
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

            username = ((string)body["username"]).ToLowerInvariant();
            password = body["password"];
            return true;
        }

        private static bool GetUsernameFromRequest(string req, out string username)
        {
            username = null;

            var body = JsonConvert.DeserializeObject<dynamic>(req);
            if (body["username"] == null)
                return false;

            username = ((string)body["username"]).ToLowerInvariant();
            return true;
        }

        private static bool GetPersonalBestFromRequest(string req, out int? personalBest, out string personalBestToken)
        {
            personalBest = null;
            personalBestToken = null;
            var body = JsonConvert.DeserializeObject<dynamic>(req);

            if (body["pb"] == null || body["pbToken"] == null)
                return false;

            personalBest = body["pb"];
            personalBestToken = body["pbToken"];
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
