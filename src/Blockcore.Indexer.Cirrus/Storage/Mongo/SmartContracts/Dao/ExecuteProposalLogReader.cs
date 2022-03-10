using System;
using System.Linq;
using Blockcore.Indexer.Cirrus.Client.Types;
using Blockcore.Indexer.Cirrus.Storage.Mongo.Types;

namespace Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts.Dao;

class ExecuteProposalLogReader : ILogReader
{
   public bool CanReadLogForMethodType(string methodType) => methodType == "ExecuteProposal";

   public bool IsTheTransactionLogComplete(LogResponse[] logs) => true;

   public void UpdateContractFromTransactionLog(CirrusContractTable contractTransaction,
      DaoContractComputedTable computedTable)
   {
      var log = contractTransaction.Logs.First().Log.Data;

      computedTable.CurrentAmount -= (long)log["amount"];

      int proposalId = (int)(long)log["proposalId"];

      var proposal = computedTable.Proposals[proposalId - 1];

      if (proposal.Id != proposalId || proposal.recipient != (string)log["recipent"])
         throw new ArgumentException(nameof(proposalId));

      proposal.WasProposalAccepted = true;
      proposal.ProposalCompletedAtBlock = contractTransaction.BlockIndex;
   }
}
