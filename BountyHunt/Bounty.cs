using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wolfje.Plugins.SEconomy;

namespace BountyHunt
{
	public class Bounty
	{
		public string name;
		public string contractor;
		public string target;
		public List<BHItem> reward;
		public List<string> hunter;
		public List<string> failures;

		public Bounty(string name, string contractor, string target, List<BHItem> reward, List<string> hunter, List<string> failures)
		{
			this.name = name;
			this.contractor = contractor;
			this.target = target;
			this.reward = reward;
			this.hunter = hunter;
			this.failures = failures;
		}
	}

	public class BHItem
	{
		public int id;
		public int stack;
		public Money money;
		public int prefix;

		public BHItem(int id, int stack, int prefix)
		{
			this.id = id;
			this.stack = stack;
			this.prefix = prefix;
		}

		public BHItem(int id, Money money, int prefix)
		{
			this.id = id;
			this.money = money;
			this.prefix = prefix;
		}
	}
}
