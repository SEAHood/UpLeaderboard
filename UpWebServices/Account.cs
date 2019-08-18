using System;
using Newtonsoft.Json;

namespace UpWebServices
{
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
}