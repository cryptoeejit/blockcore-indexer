using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blockcore.Indexer.Cirrus.Client;
using Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts.Dao;
using Blockcore.Indexer.Cirrus.Storage.Mongo.Types;
using Blockcore.Indexer.Core.Client;
using Blockcore.Indexer.Core.Operations.Types;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Blockcore.Indexer.Cirrus.Storage.Mongo;

public class DaoContractAggregator : IDAOContractAggregator
{
   const string DaoContract = "DAOContract";

   readonly ILogger<DaoContractAggregator> logger;
   readonly CirrusMongoData mongoData;
   readonly ILogReaderFactory logReaderFactory;
   readonly CirrusClient cirrusClient;

   public DaoContractAggregator(ILogger<DaoContractAggregator> logger, ICirrusStorage mongoData, ILogReaderFactory logReaderFactory, ICryptoClientFactory clientFactory, SyncConnection connection)
   {
      this.mongoData = (CirrusMongoData)mongoData;
      this.logReaderFactory = logReaderFactory;
      this.logger = logger;

      cirrusClient = clientFactory.Create(connection) as CirrusClient;
   }

   public async Task<DaoContractComputedTable> ComputeDaoContractForAddressAsync(string address)
   {
      var contract = await mongoData.DaoContractComputedTable
         .AsQueryable()
         .SingleOrDefaultAsync(_ => _.ContractAddress == address);

       if (contract is null)
      {
         var contractCode = await mongoData.CirrusContractCodeTable
            .AsQueryable()
            .SingleOrDefaultAsync(_ => _.ContractAddress == address);

         if (contractCode is null || contractCode.CodeType != DaoContract)
         {
            logger.LogInformation($"Request to compute DAO contract for address {address} which was not found in the contract code table");
            return null;
         }

         contract = await CreateNewDaoContract(address);

         if (contract is null)
            throw new ArgumentNullException($"Contract not found in the contract table for address {address}");
      }

      var contractTransactions = await mongoData.CirrusContractTable
         .AsQueryable()
         .Where(_ => _.ToAddress == address && _.Success && _.BlockIndex > contract.LastProcessedBlockHeight)
         .ToListAsync();

      if (contractTransactions.Any())
      {
         await AddNewTransactionsDataToDocumentAsync(address, contractTransactions, contract);
      }

      return contract;
   }

   private async Task<DaoContractComputedTable> CreateNewDaoContract(string address)
   {
      var contractCreationTransaction = await mongoData.CirrusContractTable
         .AsQueryable()
         .Where(_ => _.NewContractAddress == address)
         .SingleOrDefaultAsync();

      if (contractCreationTransaction is null)
         throw new ArgumentNullException(nameof(contractCreationTransaction));

      var contract = new DaoContractComputedTable
      {
         ContractAddress = contractCreationTransaction.NewContractAddress,
         LastProcessedBlockHeight = contractCreationTransaction.BlockIndex
      };

      await mongoData.DaoContractComputedTable.FindOneAndReplaceAsync<DaoContractComputedTable>(
         _ => _.ContractAddress == address, contract,
         new FindOneAndReplaceOptions<DaoContractComputedTable> { IsUpsert = true },
         CancellationToken.None);

      return contract;
   }

   private async Task AddNewTransactionsDataToDocumentAsync(string address, List<CirrusContractTable> contractTransactions,
      DaoContractComputedTable contract)
   {
      foreach (var contractTransaction in contractTransactions)
      {
         var reader = logReaderFactory.GetLogReader(contractTransaction.MethodName);

         if (reader is null)
         {
            Console.WriteLine(contractTransaction.MethodName);
            continue; //TODO need to verify this is the right way to go
         }

         if (!reader.IsTheTransactionLogComplete(contractTransaction.Logs))
         {
            var result = await cirrusClient.GetContractInfoAsync(contractTransaction.TransactionId);

            contractTransaction.Logs = result.Logs;
         }

         reader.UpdateContractFromTransactionLog(contractTransaction, contract);

         contract.LastProcessedBlockHeight = contractTransaction.BlockIndex;
      }

      await mongoData.DaoContractComputedTable.FindOneAndReplaceAsync<DaoContractComputedTable>(
         _ => _.ContractAddress == address, contract,
         new FindOneAndReplaceOptions<DaoContractComputedTable> { IsUpsert = true },
         CancellationToken.None);
   }
}
