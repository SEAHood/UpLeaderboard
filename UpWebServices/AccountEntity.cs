using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace UpWebServices
{
    public class AccountEntity : TableEntity
    {
        public AccountEntity(string username)
        {
            PartitionKey = username[0].ToString().ToUpperInvariant();
            RowKey = username;
        }

        public AccountEntity() { }

        [IgnoreProperty]
        public string Username => RowKey;

        public string Password { get; set; }

        public int PersonalBest { get; set; }

        public DateTime PersonalBestTimestamp { get; set; }
    }
}
