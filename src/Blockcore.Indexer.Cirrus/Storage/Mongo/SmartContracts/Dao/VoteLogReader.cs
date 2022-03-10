using System;
using System.Collections.Generic;
using System.Linq;
using Blockcore.Indexer.Cirrus.Client.Types;
using Blockcore.Indexer.Cirrus.Storage.Mongo.Types;

namespace Blockcore.Indexer.Cirrus.Storage.Mongo.SmartContracts.Dao;

class VoteLogReader : ILogReader
{
   public bool CanReadLogForMethodType(string methodType) => methodType == "Vote";

   public bool IsTheTransactionLogComplete(LogResponse[] logs) => true;

   public void UpdateContractFromTransactionLog(CirrusContractTable contractTransaction,
      DaoContractComputedTable computedTable)
   {
      if (contractTransaction.Logs.Length == 0)
      {
         //TODO need to handle this wierd issue (example transaction id - 5faa9c5347ba378ea1b4dd9e957e398867f724a6ba4951ed65cde6529dbfd6a0)
         return;
      }

      int id = (int)(long)contractTransaction.Logs.First().Log.Data["proposalId"];
      bool voteYesNo = (bool)contractTransaction.Logs.First().Log.Data["vote"];
      string voterAddress = (string)contractTransaction.Logs.First().Log.Data["voter"];

      var proposal = computedTable.Proposals.SingleOrDefault(_ => _.Id == id);

      if (proposal is null)
      {
         throw new InvalidOperationException(
            $"Proposal {id} not found for the vote transaction id - {contractTransaction.TransactionId}");
      }

      var vote = proposal.Votes.FirstOrDefault(_ => _.VoterAddress == voterAddress);

      if (vote != null)
      {
         vote.PreviousVotes ??= new List<DaoContractVote>();
         vote.PreviousVotes.Add(new DaoContractVote { IsApproved = vote.IsApproved,VotedOnBlock = vote.VotedOnBlock });
         vote.IsApproved = voteYesNo;
         vote.VotedOnBlock = contractTransaction.BlockIndex;
      }
      else
      {
         proposal.Votes.Add(new DaoContractVoteDetails
         {
            IsApproved = voteYesNo,
            ProposalId = id,
            VoterAddress = voterAddress,
            VotedOnBlock = contractTransaction.BlockIndex,
         });
      }
   }
}
