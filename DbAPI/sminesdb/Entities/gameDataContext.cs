using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Configuration;
using System.Linq;
using sminesdb.Base;
using sminesdb.Entities;
using sminesdb.Entities.Principal;

namespace sminesdb.Entities
{
	public partial class gameDataContext : IDisposable
	{
		private ConnectionContainer _connectionContainer;

		public ConnectionContainer DbConnection
		{
			get { return _connectionContainer; }
			set { _connectionContainer = value; }
		}

		private CashoutDap _Cashout;
		public CashoutDap Cashout
		{
			get
			{
				if (_Cashout == null)
				{
					_Cashout = new CashoutDap(_connectionContainer, true);
				}
				return _Cashout;
			}
		}

		public gameDataContext()
		{
			DbConnection = new ConnectionContainer()
									  {
										  Connection = GameDataConnection.CreateConnection()
									  };
		}

		public IDbTransaction BeginTransaction()
		{
			DbConnection.Transaction = DbConnection.Connection.BeginTransaction();
			return DbConnection.Transaction;
		}

		public IDbTransaction BeginTransaction(IsolationLevel isolationLevel)
		{
			DbConnection.Transaction = DbConnection.Connection.BeginTransaction(isolationLevel);
			return DbConnection.Transaction;
		}

		public void CommitTransaction()
		{
			DbConnection.Transaction.Commit();
		}

		public void Dispose()
		{

			if (_Cashout != null)
				_Cashout.Dispose();
			_Cashout = null;

			if (_connectionContainer != null)
				_connectionContainer.Dispose();
			_connectionContainer = null;
		}
	}
}
