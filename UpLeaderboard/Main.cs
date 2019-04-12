using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace UpLeaderboard
{
    public static class Main
    {
        [FunctionName("Leaderboard")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            /*
            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
                */
            var myObj = new {
                scores = new List<object>
                {
                    new { name = "Sam", score = 9999, rank = 1 },
                    new { name = "Ted", score = 5412, rank = 2 },
                    new { name = "Red", score = 3214, rank = 3 },
                    new { name = "Jed", score = 1002, rank = 4 },
                    new { name = "Ced", score = 485, rank = 5 }
                }
            };
            var jsonToReturn = JsonConvert.SerializeObject(myObj);

            return new OkObjectResult(jsonToReturn);

        }
    }
}
