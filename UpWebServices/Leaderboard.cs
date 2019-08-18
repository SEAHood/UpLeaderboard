using System.Collections.Generic;
using System.Linq;
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
}