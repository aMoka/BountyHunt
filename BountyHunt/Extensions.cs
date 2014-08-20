using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BountyHunt
{
	public static class Extensions
	{
		public static BHPlayer AddToList(this List<BHPlayer> list, BHPlayer item)
		{
			if (!list.Contains(item))
				list.Add(item);
			return item;
		}
	}
}
