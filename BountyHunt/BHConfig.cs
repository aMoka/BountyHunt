using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Wolfje.Plugins.SEconomy;

namespace BountyHunt
{
	public class BHConfig
	{
		public bool WarnTarget = true;
		public bool BroadcastNewBounty = false;
		public bool BroadcastHunt = false;
		public bool BroadcastSuccess = false;
		public bool AddDeathPenaltyToRewards = true;
		public Money HunterDeathPenalty = 0;
		public int MaxHuntersPerBounty = 1;
		public int MaxBountiesPerHunter = 0;
		public int MaxAttemptsPerBounty = 1;

		public static BHConfig Read(string path)
		{
			if (!File.Exists(path))
				return new BHConfig();
			using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				return Read(fs);
			}
		}

		public static BHConfig Read(Stream stream)
		{
			using (var sr = new StreamReader(stream))
			{
				var cf = JsonConvert.DeserializeObject<BHConfig>(sr.ReadToEnd());
				if (ConfigRead != null)
					ConfigRead(cf);
				return cf;
			}
		}

		public void Write(string path)
		{
			using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
			{
				Write(fs);
			}
		}

		public void Write(Stream stream)
		{
			var str = JsonConvert.SerializeObject(this, Formatting.Indented);
			using (var sw = new StreamWriter(stream))
			{
				sw.Write(str);
			}
		}

		public static Action<BHConfig> ConfigRead;
	}
}
