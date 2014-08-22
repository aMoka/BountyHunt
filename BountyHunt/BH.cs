using System;
using System.Data;
using System.IO;
using System.IO.Streams;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Wolfje.Plugins.SEconomy;

namespace BountyHunt
{
	[ApiVersion(1, 16)]
    public class BH : TerrariaPlugin
    {
		private IDbConnection _db;
		public static Database dbManager;
		public static List<BHPlayer> BHPlayers = new List<BHPlayer>();
		public static List<Bounty> bounties = new List<Bounty>();
		public static BHConfig config { get; set; }
		public static string configDir { get { return Path.Combine(TShock.SavePath, "PluginConfigs"); } }
		public static string configPath { get { return Path.Combine(configDir, "AllRecipes.json"); } }

		#region Info
		public override string Name { get { return "BountyHunt"; } }
		public override string Author { get { return "aMoka"; } }
		public override string Description { get { return "Dirty money. No, not that kind of dirty."; } }
		public override Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
		#endregion

		#region Initialize
		public override void Initialize()
		{
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.GamePostInitialize.Register(this, PostInitialize);
			ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
			ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
		}
		#endregion

		#region Dispose
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.GamePostInitialize.Deregister(this, PostInitialize);
				ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
			}
			base.Dispose(disposing);
		}
		#endregion

		#region OnInitialize
		private void OnInitialize(EventArgs args)
		{
			#region Commands
			Commands.ChatCommands.Add(new Command("bh.player.newbounty", NewBounty, "newbounty", "nbty")
			{
				HelpText = "/newbounty <bounty name/-setreward/-confirm/-cancel> <target/[SEconomy currency amount]>"
			});
			Commands.ChatCommands.Add(new Command("bh.player.bounty", GenBounty, "bounty", "bty")
			{
				HelpText = "/bounty <-list/-info/-accept> <page/bounty name>"
			});
			Commands.ChatCommands.Add(new Command("bh.admin.deletebounty", DeleteBounty, "delbounty", "dbty")
			{
				HelpText = "/delbounty <bounty>"
			});
			Commands.ChatCommands.Add(new Command("bh.admin.reload", BHReload, "bhrld")
			{
				HelpText = "Reloads BHConfig.json"
			});
			#endregion

			#region Database

			switch (TShock.Config.StorageType.ToLower())
			{
				case "sqlite":
					_db = new SqliteConnection(string.Format("uri=file://{0},Version=3",
						Path.Combine(TShock.SavePath, "Bounties.sqlite")));
					break;
				case "mysql":
					try
					{
						var host = TShock.Config.MySqlHost.Split(':');
						_db = new MySqlConnection
						{
							ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4}",
								host[0],
								host.Length == 1 ? "3306" : host[1],
								TShock.Config.MySqlDbName,
								TShock.Config.MySqlUsername,
								TShock.Config.MySqlPassword
								)
						};
					}
					catch (MySqlException x)
					{
						Log.Error(x.ToString());
						throw new Exception("MySQL not setup correctly.");
					}
					break;
				default:
					throw new Exception("Invalid storage type.");
			}
			#endregion

			dbManager = new Database(_db);
			Utils.SetUpConfig();
		}
		#endregion

		#region PostInitialize
		private void PostInitialize(EventArgs args)
		{
			Database.InitialSyncBounties();
		}
		#endregion

		#region OnGreet
		private void OnGreet(GreetPlayerEventArgs args)
		{
			BHPlayers.Add(new BHPlayer(args.Who));

			var player = TShock.Players[args.Who];
			var RecPlayer = BHPlayers.AddToList(new BHPlayer(args.Who));
		}
		#endregion

		#region OnLeave
		private void OnLeave(LeaveEventArgs args)
		{
			var player = Utils.GetPlayer(args.Who);

			BHPlayers.RemoveAll(pl => pl.Index == args.Who);
		}
		#endregion

		#region OnGetData
		private void OnGetData(GetDataEventArgs args)
		{
			var player = Utils.GetPlayer(args.Msg.whoAmI);

			#region ItemDrop
			if (args.MsgID == PacketTypes.ItemDrop)
			{
				if (args.Handled || !player.listingReward)
					return;

				using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
				{
					Int16 id = data.ReadInt16();
					float posx = data.ReadSingle();
					float posy = data.ReadSingle();
					float velx = data.ReadSingle();
					float vely = data.ReadSingle();
					Int16 stacks = data.ReadInt16();
					int prefix = data.ReadByte();
					bool nodelay = data.ReadBoolean();
					Int16 netid = data.ReadInt16();

					Item item = new Item();
					item.SetDefaults(netid);

					Console.WriteLine(String.Join(", ", id, stacks, prefix, netid));

					if (stacks == 0)
						return;

					player.droppedItems.Add(new BHItem(netid, stacks, prefix));
					player.TSPlayer.SendInfoMessage("{0} {1}{2} has been added to bounty rewards.", 
						stacks, 
						(prefix == 0) ? "" : TShock.Utils.GetPrefixByIdOrName(prefix.ToString())[0].ToString() + " ",
						item.name);
					args.Handled = true;
				}
			}
			#endregion

			#region PlayerDamage
			if (args.MsgID == PacketTypes.PlayerDamage)
			{
				if (args.Handled)
					return;

				using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
				{
					var playerID = data.ReadByte();
					var hitDirection = data.ReadByte();
					var damage = data.ReadInt16();
					var pvp = data.ReadBoolean();
					var crit = data.ReadBoolean();

					Utils.GetPlayer(playerID).killingPlayer = (args.Msg.whoAmI != playerID) ? Utils.GetPlayer(args.Msg.whoAmI) : null;
				}
			}
			#endregion

			#region KillMe
			if (args.MsgID == PacketTypes.PlayerKillMe)
			{
				if (args.Handled)
					return;

				using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
				{
					var playerId = data.ReadByte();
					var hitDirection = data.ReadByte();
					var damage = data.ReadInt16();
					var pvp = data.ReadBoolean();

					var plr = Utils.GetPlayer(playerId);

					if (plr.killingPlayer != null)
					{
						var killer = plr.killingPlayer;
						if (pvp)
						{
							if (Utils.CheckVictimWasTarget(plr.name, killer.activeBounties))
							{
								List<Bounty> completedBounties = new List<Bounty>();
								foreach (Bounty bounty in killer.activeBounties.Keys)
								{
									if (bounty.target == plr.name)
									{
										for (int i = 0; i < bounty.reward.Count - 1; i++)
										{
											Item item = new Item();
											item.SetDefaults(bounty.reward[i].id);
											killer.TSPlayer.GiveItem(item.netID, item.name, item.width, item.height, bounty.reward[i].stack, bounty.reward[i].prefix);
										}
										if (bounty.reward[bounty.reward.Count - 1].money != 0)
										{
											SEconomyPlugin.Instance.WorldAccount.TransferToAsync(
												SEconomyPlugin.Instance.GetBankAccount(killer.TSPlayer.UserAccountName), 
												bounty.reward[bounty.reward.Count - 1].money, 
												Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver,
												String.Format("for the bounty rewards.", bounty.reward[bounty.reward.Count - 1].money),
												String.Format("BountyHunt: " + "receiving money for reward."));
										}
										completedBounties.Add(bounty);
									}
								}
								for (int i = 0; i < completedBounties.Count; i++)
								{
									foreach (var hunter in completedBounties[i].hunter)
									{
										var foundplr = TShock.Utils.FindPlayer(hunter);
										foundplr[0].SendInfoMessage("{0} completed {1}, a bounty you accepted.", killer.name, completedBounties[i].name);
										foundplr[0].SendInfoMessage("{0} will be removed from your accepted bounties.", completedBounties[i].name);
									}
									bounties.Remove(completedBounties[i]);
									dbManager.DeleteBounty(completedBounties[i]);
								}
							}
							if (Utils.CheckVictimWasHunter(plr.name, plr.activeBounties))
							{
								List<Bounty> failedBounties = new List<Bounty>();
								foreach (Bounty bounty in plr.activeBounties.Keys)
								{
									if (bounty.target == killer.name)
									{
										plr.activeBounties[bounty]++;
										if (config.MaxAttemptsPerBounty != 0 && plr.activeBounties[bounty] >= config.MaxAttemptsPerBounty)
										{
											failedBounties.Add(bounty);
										}
										if (config.HunterDeathPenalty != 0)
										{
											SEconomyPlugin.Instance.GetBankAccount(plr.TSPlayer.UserAccountName).TransferToAsync(
												SEconomyPlugin.Instance.WorldAccount,
												config.HunterDeathPenalty,
												Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToSender,
												String.Format("dying to your bounty target."),
												String.Format("BountyHunt: " + "lost money for death on the hunt."));
											if (config.AddDeathPenaltyToRewards)
												bounties[bounties.IndexOf(bounty)].reward[bounties[bounties.IndexOf(bounty)].reward.Count].money += config.HunterDeathPenalty;
										}
									}
								}
								for (int i = 0; i < failedBounties.Count; i++)
								{
									plr.TSPlayer.SendErrorMessage("You have failed the {0} bounty {1} time(s).", failedBounties[i].name, plr.activeBounties[failedBounties[i]]);
									plr.TSPlayer.SendErrorMessage("{0} will be removed from your accept bounties, and can no longer be accepted by you.", failedBounties[i]);
									
									foreach (Bounty bounty in bounties)
									{
										if (bounty == failedBounties[i])
										{
											bounty.hunter.Remove(plr.name);
											bounty.failures.Add(plr.name);
											dbManager.UpdateHunters(bounty);
											dbManager.UpdateFailures(bounty);
										}
									}
								}
							}
						}
						plr.killingPlayer = null;
					}
				}
			}
			#endregion
		}
		#endregion

		public BH(Main game)
            : base(game) 
		{
			Order = -1;

			config = new BHConfig();
		}

		#region Commands
		#region NewBounty
		private void NewBounty(CommandArgs args)
		{
			var player = Utils.GetPlayer(args.Player.Index);
			if (args.Parameters.Count < 1)
			{
				Utils.InvalidNewBountyUsage(args.Player);
				return;
			}
			var UserSEAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.UserAccountName);
			var playeramount = UserSEAccount.Balance;
			var subcmd = args.Parameters[0].ToLower();

			switch (subcmd)
			{
				case "-setrewards":
					if (!player.listingBounty)
					{
						args.Player.SendErrorMessage("You need to start a bounty!");
						return;
					}
					Money money = 0;
					if (args.Parameters.Count > 1)
					{
						if (!Money.TryParse(args.Parameters[1], out money))
						{
							Utils.InvalidNewBountyUsage(args.Player);
							return;
						}
						player.bountyAmount = money;
						args.Player.SendInfoMessage("SEconomy bounty reward set to {0}!", player.bountyAmount.ToString());
						if (player.listingReward)
							return;
					}
					if (player.listingReward)
					{
						args.Player.SendErrorMessage("You are already setting rewards for this bounty!");
						return;
					}
					player.listingReward = true;
					args.Player.SendInfoMessage("Drop any reward items for your bounty.");
					break;
				case "-confirm":
					if (player.droppedItems.Count < 1 && player.bountyAmount == 0)
					{
						args.Player.SendErrorMessage("A bounty without a reward is no bounty. Specify a reward!");
						player.droppedItems.Clear();
						return;
					}
					if (player.bountyAmount != 0)
					{
						UserSEAccount.TransferToAsync(
							SEconomyPlugin.Instance.WorldAccount, 
							player.bountyAmount,
							Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToSender,
							String.Format("the bounty rewards."),
							String.Format("BountyHunt: " + "Adding money to reward."));
					}
					Console.WriteLine(player.bountyAmount.ToString());
					player.droppedItems.Add(new BHItem(0, player.bountyAmount, 0));
					dbManager.AddNewBounty(new Bounty(
						player.bountyName, 
						player.name, 
						player.bountyTarget, 
						player.droppedItems, 
						new List<string> { "" }, 
						new List<string> { "" }));
					if (config.BroadcastNewBounty)
					{
						TSPlayer.All.SendInfoMessage("A new bounty ({0}) was listed by {1}{2}",
							player.bountyName,
							player.name,
							(config.WarnTarget) ? ", targeting " + player.bountyTarget : "");
						TSPlayer.All.SendInfoMessage("with rewards ({0}).", Utils.ItemListToRewardsString(player.droppedItems));
					}
					else
					{
						args.Player.SendInfoMessage("Bounty ({0}) was listed targeting {1}",
							player.bountyName,
							player.bountyTarget);
						args.Player.SendInfoMessage("with rewards ({0}).", Utils.ItemListToRewardsString(player.droppedItems));
					}
					if (config.WarnTarget)
					{
						var target = TShock.Utils.FindPlayer(player.bountyTarget);
						target[0].SendErrorMessage("You are now the target of a bounty! Good luck....");
					}

					player.listingBounty = false;
					player.listingReward = false;
					player.bountyName = String.Empty;
					player.bountyTarget = String.Empty;
					player.bountyAmount = 0;
					player.droppedItems.Clear();
					return;
				case "-cancel":
					args.Player.SendInfoMessage("Returning reward items...");
					foreach (var reward in player.droppedItems)
					{
						Item item = new Item();
						item.SetDefaults(reward.id);
						player.TSPlayer.GiveItem(item.type, item.name, item.width, item.height, reward.stack, reward.prefix);
					}
					player.listingBounty = false;
					player.listingReward = false;
					player.bountyName = String.Empty;
					player.bountyTarget = String.Empty;
					player.bountyAmount = 0;
					player.droppedItems.Clear();
					args.Player.SendSuccessMessage("Bounty listing cancelled.");
					return;
				default:
					if (player.listingBounty)
					{
						args.Player.SendErrorMessage("You are already listing a bounty!");
						args.Player.SendErrorMessage("Type \"/nb -cancel\" to cancel your current listing.");
					}
					if (args.Parameters.Count < 2)
					{
						Utils.InvalidNewBountyUsage(args.Player);
						return;
					}
					if (Utils.CheckBountyNameExists(args.Parameters[0]))
					{
						args.Player.SendErrorMessage("Bounty name already taken. Please choose a different name for your listing.");
						return;
					}
					var foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
					if (foundplr.Count < 1)
					{
						args.Player.SendErrorMessage("Invalid player!");
						return;
					}
					else if (foundplr.Count > 1)
					{
						TShock.Utils.SendMultipleMatchError(args.Player, foundplr.Select(p => p.Name));
						return;
					}
					else
					{
						player.bountyName = args.Parameters[0];
						player.bountyTarget = foundplr[0].Name;
						args.Player.SendInfoMessage("You are now listing a bounty ({0}) targeting {1}.", player.bountyName, player.bountyTarget);
						args.Player.SendInfoMessage("Type \"/nbty -setrewards [optional SEconomy reward]\" to set rewards,");
						args.Player.SendInfoMessage("or \"/nbty -cancel\" to cancel listing.");
						player.listingBounty = true;
					}
					break;
			}
		}
		#endregion

		#region GenBounty
		private void GenBounty(CommandArgs args)
		{
			var player = Utils.GetPlayer(args.Player.Index);
			if (args.Parameters.Count < 1)
			{
				Utils.InvalidGenBountyUsage(args.Player);
				return;
			}
			if (args.Parameters[0].ToLower() == "-list")
			{
				int page;
				if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out page))
					return;

				List<string> allBty = new List<string>();
				foreach (Bounty bty in bounties)
					allBty.Add(bty.name);
				PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(allBty),
					new PaginationTools.Settings
					{
						HeaderFormat = "Bounties ({0}/{1}):",
						FooterFormat = "Type /bty -list {0} for more.",
						NothingToDisplayString = "There are currently no bounties listed!"
					});
				return;
			}
			if (args.Parameters.Count < 2)
			{
				Utils.InvalidGenBountyUsage(args.Player);
				return;
			}
			if (!Utils.CheckBountyNameExists(args.Parameters[1]))
			{
				args.Player.SendErrorMessage("Invalid bounty!");
				return;
			}
			for (int i = 0; i < bounties.Count; i++)
			{
				if (args.Parameters[1].ToLower() == bounties[i].name.ToLower())
				{
					var subcmd = args.Parameters[0].ToLower();

					switch (subcmd)
					{
						case "-info":
							args.Player.SendInfoMessage("Bounty ({0}) was listed by {1} targeting {2}",
							bounties[i].name,
							bounties[i].contractor,
							bounties[i].target);
							args.Player.SendInfoMessage("with rewards ({0}).", Utils.ItemListToRewardsString(bounties[i].reward));
							return;
						case "-accept":
							if (bounties[i].hunter.Contains(args.Player.Name))
							{
								args.Player.SendErrorMessage("You have already accepted this bounty!");
								return;
							}
							if (bounties[i].failures.Contains(args.Player.Name))
							{
								args.Player.SendErrorMessage("You have failed this bounty already!");
								args.Player.SendErrorMessage("You can no longer accept this bounty.");
								return;
							}
							if (config.MaxHuntersPerBounty != 0 && bounties[i].hunter.Count >= config.MaxHuntersPerBounty)
							{
								args.Player.SendErrorMessage("The maximum number of hunters have accepted this bounty!");
								args.Player.SendErrorMessage("Please try again later.");
								return;
							}
							if (config.MaxBountiesPerHunter != 0 && player.activeBounties.Count >= config.MaxBountiesPerHunter)
							{
								args.Player.SendErrorMessage("You have taken the maximum number of bounties!");
								args.Player.SendErrorMessage("Abandon another bounty to accept this bounty.");
								return;
							}
							player.activeBounties.Add(bounties[i], 0);
							bounties[i].hunter.Add(args.Player.Name);
							dbManager.UpdateHunters(bounties[i]);
							args.Player.SendSuccessMessage("You have accepted \"{0}.\" Your target is \"{1}.\"", bounties[i].name, bounties[i].target);
							return;
						case "-abandon":
							Bounty btyToRemove = null;
							foreach (Bounty bounty in player.activeBounties.Keys)
							{
								if (bounty.name == bounties[i].name)
									btyToRemove = bounty;
							}
							if (btyToRemove != null)
								player.activeBounties.Remove(btyToRemove);
							bounties[i].hunter.Remove(args.Player.Name);
							dbManager.UpdateHunters(bounties[i]);
							args.Player.SendInfoMessage("You have abandoned \"{0}.\"", bounties[i].name);
							return;
						default:
							Utils.InvalidGenBountyUsage(args.Player);
							return;
					}
				}
			}
		}
		#endregion

		#region DeleteBounty
		private void DeleteBounty(CommandArgs args)
		{
			if (args.Parameters.Count < 1)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /dbty <bounty name>.");
				return;
			}

			for (int i = 0; i < bounties.Count; i++)
			{
				if (args.Parameters[0].ToLower() == bounties[i].name.ToLower())
				{
					args.Player.SendSuccessMessage("Removed {0} from the bounty listings.", bounties[i].name);
					bounties.RemoveAt(i);
					dbManager.DeleteBounty(bounties[i]);
					break;
				}
			}
		}
		#endregion

		#region BHReload
		private void BHReload(CommandArgs args)
		{
			args.Player.SendInfoMessage("Attempting to reload BHConfig.json...");
			if (Utils.SetUpConfig())
				args.Player.SendSuccessMessage("BHConfig.json reloaded successfully!");
			else
				args.Player.SendErrorMessage("Error in BHConfig.json!");
		}
		#endregion
		#endregion
	}
}
