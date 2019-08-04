using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace UpWebServices
{
    public class Repo
    {
        private readonly CloudTableClient _tableClient;

        public Repo(CloudStorageAccount storageAccount)
        {
            _tableClient = storageAccount.CreateCloudTableClient();
        }
        
        public async Task<List<AccountEntity>> GetAccounts()
        {
            var accounts = new List<AccountEntity>();
            var accountTable = _tableClient.GetTableReference("account");
            await accountTable.CreateIfNotExistsAsync();

            if (await accountTable.ExistsAsync())
            {
                var query = new TableQuery<AccountEntity>();

                TableContinuationToken token = null;
                do
                {
                    var resultSegment = await accountTable.ExecuteQuerySegmentedAsync(query, token);
                    token = resultSegment.ContinuationToken;

                    foreach (var account in resultSegment.Results)
                    {
                        account.PersonalBestToken = null; // Don't want to expose this
                        accounts.Add(account);
                    }
                } while (token != null);
            }

            return accounts;
        }
        
        public async Task<AccountEntity> GetAccount(string username)
        {
            var accountTable = _tableClient.GetTableReference("account");
            await accountTable.CreateIfNotExistsAsync();

            var getOp = TableOperation.Retrieve<AccountEntity>(username[0].ToString().ToUpperInvariant(), username);
            var result = await accountTable.ExecuteAsync(getOp);

            return (AccountEntity)result.Result;
        }

        public async Task CreateAccount(AccountEntity account)
        {
            var accountTable = _tableClient.GetTableReference("account");
            await accountTable.CreateIfNotExistsAsync();

            var insertOp = TableOperation.Insert(account);
            await accountTable.ExecuteAsync(insertOp);
        }

        public async Task UpdateAccount(AccountEntity account)
        {
            var accountTable = _tableClient.GetTableReference("account");
            await accountTable.CreateIfNotExistsAsync();

            var mergeOp = TableOperation.Merge(account);
            await accountTable.ExecuteAsync(mergeOp);
        }
    }
}
