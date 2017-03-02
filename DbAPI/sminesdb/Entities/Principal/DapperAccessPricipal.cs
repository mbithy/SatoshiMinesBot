using System.Collections.Generic;
using System.Linq;
using sminesdb.Base;

namespace sminesdb.Entities.Principal
{

	public partial class CashoutDap : BaseDap
	{
        public string Connection { get; set; }
		public CashoutDap()
		{
		}
		public CashoutDap(BaseDap dapProvider)
		{
			if (dapProvider != null &&
				dapProvider.ConnectionContainer != null &&
				dapProvider.ConnectionContainer.Connection != null)
			{
				LeaveOpen = dapProvider.LeaveOpen;
				ConnectionContainer = dapProvider.ConnectionContainer;
			}
		}
		public CashoutDap(ConnectionContainer connectionContainer, bool leaveOpen)
		{
			if (connectionContainer != null &&
				connectionContainer.Connection != null)
			{
				LeaveOpen = leaveOpen;
				ConnectionContainer = connectionContainer;
			}
		}

		/// <summary>
		/// Returns all records from the table.
		/// Please be aware that any predicate cannot be applied to the returned IEnumurable and it will allways read all records.
		/// </summary>
		/// <returns>Delayed read all records from the database table.</returns>
		public IEnumerable<Cashout> All()
		{
			return Query<Cashout>(SqlSelectCommand, buffered: false);
		}

		public List<Cashout> GetTop(int count)
		{
			var queryResult = Query<Cashout>(string.Format("SELECT TOP {0} * FROM {1}", count, SqlTableName));
			return queryResult as List<Cashout> ?? queryResult.ToList();
		}

		public void Insert(Cashout model)
		{
			Execute(SqlInsertCommand, model);
		}

		public void Insert(IEnumerable<Cashout> models)
		{
			Execute(SqlInsertCommand, models);
		}
        public long NextId()
        {
            var x = Query<Cashout>(SqlSelectCommand + " ORDER BY id DESC LIMIT 1").FirstOrDefault();
            if (x == null)
            {
                return 1;
            }
            else
            {
                return x.Id + 1;
            }
        }



        public const string SqlTableName = "cashout";
		public const string SqlSelectCommand = "SELECT * FROM " + SqlTableName;
		public const string SqlInsertCommand = "INSERT INTO " + SqlTableName + "(id , mines , randomString , gameId , win , gameLink ,balanceAfter, lastUpdate) VALUES( @Id , @Mines , @RandomString , @GameId , @Win , @GameLink ,@BalanceAfter, @LastUpdate) ";
		
	}

}
