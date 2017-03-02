using System;
using System.ComponentModel.DataAnnotations;
using sminesdb.Base;

namespace sminesdb.Entities.Principal
{

	public partial class Cashout : BaseModel
	{

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public Int64 Id { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public String Mines { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public String RandomString { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public String GameId { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public Int64? Win { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public String GameLink { get; set; }

        public string BalanceAfter { get; set; }

		/// <summary>
		/// 
		/// </summary>
		[Display(Name = "")]
		public Decimal? LastUpdate { get; set; }


	}

}
