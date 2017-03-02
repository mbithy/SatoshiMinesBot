using System;
using System.Data;

namespace sminesdb.Entities
{
	public class ConnectionContainer : IDisposable
	{
		private IDbConnection _connection { get; set; }

		public IDbConnection Connection
		{
			get { return _connection; }
			set { _connection = value; }
		}

		private IDbTransaction _transaction;

		public IDbTransaction Transaction
		{
			get { return _transaction; }
			set
			{
				_transaction = value;
				if (_transaction != null)
					_connection = _transaction.Connection;
			}
		}

		public void Dispose()
		{
			if (_transaction != null)
				_transaction.Dispose();

			if (_connection != null)
				_connection.Dispose();

			_transaction = null;
			_connection = null;
		}
	}
}
