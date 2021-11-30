namespace Blockcore.Indexer.Storage.Mongo.Types
{
   using System.Collections.Generic;

   public class AddressComputed
   {
      public string Id { get; set; }

      public string Address { get; set; }

      public long Available { get; set; }

      public long Received { get; set; }

      public long Sent { get; set; }

      public long Staked { get; set; }

      public long Mined { get; set; }

      public long ComputedBlockIndex { get; set; }

      public long CountReceived { get; set; }

      public long CountSent { get; set; }

      public long CountStaked { get; set; }

      public long CountMined { get; set; }
   }
}