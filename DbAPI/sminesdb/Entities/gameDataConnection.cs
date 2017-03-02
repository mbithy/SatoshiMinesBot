using System;
using System.Data.SQLite;

namespace sminesdb.Entities
{
	public class GameDataConnection
	{
		

        public static SQLiteConnection CreateConnection()
        {
            var appPath = AppDomain.CurrentDomain.BaseDirectory;
            var con = "data source=" + appPath + "gameData.sqlite;Default Timeout=30";
            var connection = new SQLiteConnection(con);
            connection.SetPassword(@"");
            return connection;
        }

    }
}
