using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Data;
using sminesdb.Entities;

namespace sminesdb.Base
{
	/// <summary>
	/// Assigns the owner property of the results
	/// </summary>
	class DapperModelQuery<T> : IEnumerable<T>, IEnumerator<T>
	{
		private readonly IEnumerable<T> _query;
		private readonly ConnectionContainer _dbConnection;
		private IEnumerator<T> _queryEnumerator;

		public DapperModelQuery(IEnumerable<T> query, ConnectionContainer dbConnection)
		{
			_query = query;
			_dbConnection = dbConnection;
		}

		public IEnumerator<T> GetEnumerator()
		{
			_queryEnumerator = _query.GetEnumerator();
			return this;
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Dispose()
		{
			if (_queryEnumerator != null)
				_queryEnumerator.Dispose();
		}

		public bool MoveNext()
		{
			return _queryEnumerator.MoveNext();
		}

		public void Reset()
		{
			_queryEnumerator.Reset();
		}

		public T Current
		{
			get
			{
				var c = _queryEnumerator.Current;
				var cBase = c as BaseModel;
				if (cBase != null)
					cBase.DbConnection = _dbConnection;
				return c;
			}
		}

		object IEnumerator.Current
		{
			get { return Current; }
		}
	}
}
