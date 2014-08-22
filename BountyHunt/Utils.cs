using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace BountyHunt
{
	public class Utils
	{
		#region BHPlayerStuff
		public static BHPlayer GetPlayer(int index)
		{
			foreach (BHPlayer player in BH.BHPlayers)
				if (player.Index == index)
					return player;

			return null;
		}
		#endregion

		#region InvalidNewBountyUsage
		public static void InvalidNewBountyUsage(TSPlayer player)
		{
			player.SendErrorMessage("Invalid usage! Proper usages:");
			player.SendErrorMessage("/nbty <bounty name> <target>");
			player.SendErrorMessage("/nbty <-setrewards> [SEconomy currency amount]");
			player.SendErrorMessage("/nbty <-confirm/-cancel>");
		}
		#endregion

		#region InvalidGenBountyUsage
		public static void InvalidGenBountyUsage(TSPlayer player)
		{
			player.SendErrorMessage("Invalid usage! Proper usages:");
			player.SendErrorMessage("/bty -list [page]");
			player.SendErrorMessage("/bty <-info/-accept/-abandon> <bounty name>");
		}
		#endregion

		#region ToAndFromDatabase
		public static List<BHItem> GetRewardsFromDB(string rewards)
		{
			List<BHItem> lBHRewards = new List<BHItem>();
			List<string> lsRewards = new List<string>();
			lsRewards = rewards.Split(';').ToList();
			foreach (string s in lsRewards)
			{
				string[] item = s.Split(',');
				lBHRewards.Add(new BHItem(Convert.ToInt32(item[0]), Convert.ToInt32(item[1]), Convert.ToInt32(item[2])));
			}
			return lBHRewards;
		}

		public static string TurnRewardsToString(List<BHItem> rewards)
		{
			string strRewards = string.Empty;
			List<string> lRewards = new List<string>();
			foreach (var item in rewards)
				lRewards.Add(String.Format("{0},{1},{2}", item.id, item.stack, item.prefix));
			strRewards = String.Join(";", lRewards);
			return strRewards;
		}
		#endregion

		#region ItemListToRewardsString
		public static string ItemListToRewardsString(List<BHItem> rewards)
		{
			List<string> lRewards = new List<string>();
			for (int i = 0; i < rewards.Count - 1; i++)
			{
				lRewards.Add(String.Format("{0} {1}{2}", 
					rewards[i].stack.ToString(), 
					(rewards[i].prefix == 0) ? "" : TShock.Utils.GetPrefixById(rewards[i].prefix) + " ",
					TShock.Utils.GetItemById(rewards[i].id).name));
			}
			if (rewards[rewards.Count - 1].money != 0)
			{
				lRewards.Add(String.Format("{0} {1}", 
					rewards[rewards.Count - 1].money.ToString(),
					Wolfje.Plugins.SEconomy.Money.CurrencyName));
			}
			return String.Join(", ", lRewards);
		}
		#endregion

		#region CheckBountyNameExists
		public static bool CheckBountyNameExists(string name)
		{
			foreach (Bounty bounty in BH.bounties)
			{
				if (bounty.name.ToLower() == name.ToLower())
					return true;
			}
			return false;
		}
		#endregion

		#region CheckVictimWasTarget
		public static bool CheckVictimWasTarget(string name, Dictionary<Bounty, int> bounties)
		{
			foreach (Bounty bounty in bounties.Keys)
			{
				if (name == bounty.target)
					return true;
			}
			return false;
		}
		#endregion

		#region CheckVictimWasHunter
		public static bool CheckVictimWasHunter(string name, Dictionary<Bounty, int> bounties)
		{
			foreach (Bounty bounty in bounties.Keys)
			{
				if (bounty.hunter.Contains(name))
					return true;
			}
			return false;
		}
		#endregion

		#region SetUpConfig
		public static bool SetUpConfig()
		{
			try
			{
				if (!Directory.Exists(BH.configDir))
					Directory.CreateDirectory(BH.configDir);

				if (File.Exists(BH.configPath))
					BH.config = BHConfig.Read(BH.configPath);
				else
					BH.config.Write(BH.configPath);
				return true;
			}
			catch (Exception ex)
			{
				Log.ConsoleError("Error in BHConfig.json!");
				Log.ConsoleError(ex.ToString());
				return false;
			}
		}
		#endregion
	}
}
