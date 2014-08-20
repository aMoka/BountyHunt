using System;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using TShockAPI;
using TShockAPI.DB;

namespace BountyHunt
{
	public class Database
	{
		private readonly IDbConnection _db;

		public Database(IDbConnection db)
		{
			_db = db;

			var sqlCreator = new SqlTableCreator(db,
											 db.GetSqlType() == SqlType.Sqlite
											 ? (IQueryBuilder)new SqliteQueryCreator()
											 : new MysqlQueryCreator());

			var table = new SqlTable("Bounties",
				new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
				new SqlColumn("Name", MySqlDbType.Text) { Unique = true },
				new SqlColumn("Contractor", MySqlDbType.Text),
				new SqlColumn("Target", MySqlDbType.Text),
				new SqlColumn("Rewards", MySqlDbType.Text),
				new SqlColumn("Hunters", MySqlDbType.Text),
				new SqlColumn("Failures", MySqlDbType.Text)
				);
			sqlCreator.EnsureExists(table);
		}

		public void AddNewBounty(Bounty bounty)
		{
			try
			{
				_db.Query("INSERT INTO Bounties (Name, Contractor, Target, Rewards, Hunters, Failures)"
					+ " VALUES (@0, @1, @2, @3, @4, @5)",
					bounty.name, bounty.contractor, bounty.target, Utils.TurnRewardsToString(bounty.reward), bounty.hunter, bounty.failures);
				if (!BH.bounties.Contains(bounty))
					BH.bounties.Add(bounty);
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
			}
		}

		public void DeleteBounty(Bounty bounty)
		{
			try
			{
				_db.Query("DELETE FROM Bounties WHERE Name = @0", bounty.name);
				BH.bounties.Remove(bounty);
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
			}
		}

		public void UpdateHunters(Bounty bounty)
		{
			try
			{
				_db.Query("UPDATE Bounties SET Hunters = @0 WHERE Name = @1", String.Join(", ", bounty.hunter), bounty.name);
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
			}
		}

		public void UpdateFailures(Bounty bounty)
		{
			try
			{
				_db.Query("UPDATE Bounties SET Failures = @0 WHERE Name = @1", String.Join(", ", bounty.failures), bounty.name);
			}
			catch (Exception ex)
			{
				Log.Error(ex.ToString());
			}
		}

		public void InitialSyncBounties()
		{
			using (var reader = _db.QueryReader("SELECT * FROM Bounties"))
			{
				while (reader.Read())
				{
					var name = reader.Get<string>("Name");
					var contractor = reader.Get<string>("Contractor");
					var target = reader.Get<string>("Target");
					var rewards = reader.Get<string>("Rewards");
					var hunter = reader.Get<string>("Hunter");
					var failures = reader.Get<string>("Failures");
					BH.bounties.Add(new Bounty(name, contractor, target, Utils.GetRewardsFromDB(rewards), hunter.Split(',').ToList(), failures.Split(',').ToList()));
				}
			}
		}
	}
}
