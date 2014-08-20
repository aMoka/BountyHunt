using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;
using Wolfje.Plugins.SEconomy;

namespace BountyHunt
{
	public class BHPlayer
	{
		public int Index;

		public Dictionary<Bounty, int> activeBounties = new Dictionary<Bounty, int>();
		public BHPlayer killingPlayer = null;

		public bool listingBounty;
		public bool listingReward;
		public string bountyName;
		public string bountyTarget;
		public Money bountyAmount = 0;
		public List<BHItem> droppedItems = new List<BHItem>();

		public string name { get { return Main.player[Index].name; } }
		public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }

		public BHPlayer(int index)
		{
			Index = index;
		}
	}
}
