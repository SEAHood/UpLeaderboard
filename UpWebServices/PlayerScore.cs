using System;
using Newtonsoft.Json;

namespace UpWebServices
{
    public class PlayerScore
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("score")]
        public int Score { get; set; }
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }
}