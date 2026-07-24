using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace 黑市
{
    public class 货币数据
    {
        private string connectionString;
        private readonly object _dbLock = new object();

        public 货币数据()
        {
            黑市路径.确保目录存在();
            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = 黑市路径.数据库路径,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            }.ToString();
            初始化表();
        }

        public void Dispose()
        {
        }

        private SqliteConnection 获取连接()
        {
            var conn = new SqliteConnection(connectionString);
            conn.Open();
            return conn;
        }

        private void 初始化表()
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS 黑市货币 (
                    玩家名 TEXT NOT NULL,
                    货币名 TEXT NOT NULL,
                    数量 INTEGER DEFAULT 0,
                    PRIMARY KEY (玩家名, 货币名)
                )";
                cmd.ExecuteNonQuery();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS 黑市冷却 (
                    玩家名 TEXT NOT NULL,
                    商品序号 INTEGER NOT NULL,
                    结束时间 TEXT NOT NULL,
                    PRIMARY KEY (玩家名, 商品序号)
                )";
                cmd.ExecuteNonQuery();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS 黑市职业 (
                    玩家名 TEXT NOT NULL PRIMARY KEY,
                    当前职业 TEXT NOT NULL DEFAULT '入门'
                )";
                cmd.ExecuteNonQuery();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS 黑市任务 (
                    玩家名 TEXT NOT NULL,
                    任务序号 INTEGER NOT NULL,
                    状态 INTEGER DEFAULT 0,
                    进度 INTEGER DEFAULT 0,
                    PRIMARY KEY (玩家名, 任务序号)
                )";
                cmd.ExecuteNonQuery();
            }
        }

        public int 获取余额(string 玩家名, string 货币名)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 数量 FROM 黑市货币 WHERE 玩家名 = @玩家名 AND 货币名 = @货币名";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@货币名", 货币名);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt32(result);
            }
        }

        public void 增加货币(string 玩家名, string 货币名, int 数量)
        {
            if (数量 <= 0) return;
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO 黑市货币 (玩家名, 货币名, 数量) VALUES (@玩家名, @货币名, COALESCE((SELECT 数量 FROM 黑市货币 WHERE 玩家名 = @玩家名 AND 货币名 = @货币名), 0) + @数量)";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@货币名", 货币名);
                cmd.Parameters.AddWithValue("@数量", 数量);
                cmd.ExecuteNonQuery();
            }
        }

        public bool 扣除货币(string 玩家名, string 货币名, int 数量)
        {
            if (数量 <= 0) return true;
            lock (_dbLock)
            {
                int 当前 = 获取余额(玩家名, 货币名);
                if (当前 < 数量) return false;

                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE 黑市货币 SET 数量 = 数量 - @数量 WHERE 玩家名 = @玩家名 AND 货币名 = @货币名";
                cmd.Parameters.AddWithValue("@数量", 数量);
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@货币名", 货币名);
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        public bool 设置货币(string 玩家名, string 货币名, int 数量)
        {
            if (数量 < 0) return false;
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                if (数量 == 0)
                {
                    cmd.CommandText = "DELETE FROM 黑市货币 WHERE 玩家名 = @玩家名 AND 货币名 = @货币名";
                }
                else
                {
                    cmd.CommandText = "INSERT OR REPLACE INTO 黑市货币 (玩家名, 货币名, 数量) VALUES (@玩家名, @货币名, @数量)";
                }
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@货币名", 货币名);
                cmd.Parameters.AddWithValue("@数量", 数量);
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        public Dictionary<string, int> 获取玩家所有货币(string 玩家名)
        {
            lock (_dbLock)
            {
                var dict = new Dictionary<string, int>();
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 货币名, 数量 FROM 黑市货币 WHERE 玩家名 = @玩家名 AND 数量 > 0";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    dict[reader.GetString(0)] = reader.GetInt32(1);
                return dict;
            }
        }

        public DateTime? 获取冷却结束时间(string 玩家名, int 商品序号)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 结束时间 FROM 黑市冷却 WHERE 玩家名 = @玩家名 AND 商品序号 = @商品序号";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@商品序号", 商品序号);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return null;
                if (DateTime.TryParse(result.ToString(), out var dt))
                    return dt;
                return null;
            }
        }

        public void 设置冷却(string 玩家名, int 商品序号, int 秒数)
        {
            var 结束时间 = DateTime.Now.AddSeconds(秒数).ToString("yyyy-MM-dd HH:mm:ss");
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO 黑市冷却 (玩家名, 商品序号, 结束时间) VALUES (@玩家名, @商品序号, @结束时间)";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@商品序号", 商品序号);
                cmd.Parameters.AddWithValue("@结束时间", 结束时间);
                cmd.ExecuteNonQuery();
            }
        }

        public void 清理过期冷却()
        {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM 黑市冷却 WHERE 结束时间 < @now";
                cmd.Parameters.AddWithValue("@now", now);
                cmd.ExecuteNonQuery();
            }
        }

        public string 获取玩家职业(string 玩家名)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 当前职业 FROM 黑市职业 WHERE 玩家名 = @玩家名";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return "入门";
                return (string)result;
            }
        }

        public void 设置玩家职业(string 玩家名, string 职业名)
        {
            if (string.IsNullOrEmpty(职业名)) 职业名 = "入门";
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO 黑市职业 (玩家名, 当前职业) VALUES (@玩家名, @职业名)";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@职业名", 职业名);
                cmd.ExecuteNonQuery();
            }
        }

        // ========== 任务相关方法 ==========

        public 任务状态 获取任务状态(string 玩家名, int 任务序号)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 状态 FROM 黑市任务 WHERE 玩家名 = @玩家名 AND 任务序号 = @任务序号";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@任务序号", 任务序号);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return 任务状态.未接取;
                return (任务状态)Convert.ToInt32(result);
            }
        }

        public int 获取任务进度(string 玩家名, int 任务序号)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 进度 FROM 黑市任务 WHERE 玩家名 = @玩家名 AND 任务序号 = @任务序号";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@任务序号", 任务序号);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt32(result);
            }
        }

        public void 设置任务状态(string 玩家名, int 任务序号, 任务状态 状态, int 进度)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO 黑市任务 (玩家名, 任务序号, 状态, 进度) VALUES (@玩家名, @任务序号, @状态, @进度)";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@任务序号", 任务序号);
                cmd.Parameters.AddWithValue("@状态", (int)状态);
                cmd.Parameters.AddWithValue("@进度", 进度);
                cmd.ExecuteNonQuery();
            }
        }

        public void 删除任务进度(string 玩家名, int 任务序号)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM 黑市任务 WHERE 玩家名 = @玩家名 AND 任务序号 = @任务序号";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@任务序号", 任务序号);
                cmd.ExecuteNonQuery();
            }
        }

        public List<(int 任务序号, int 进度)> 获取玩家进行中任务(string 玩家名)
        {
            lock (_dbLock)
            {
                var list = new List<(int, int)>();
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 任务序号, 进度 FROM 黑市任务 WHERE 玩家名 = @玩家名 AND 状态 = @状态";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                cmd.Parameters.AddWithValue("@状态", (int)任务状态.进行中);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add((reader.GetInt32(0), reader.GetInt32(1)));
                return list;
            }
        }

        public int 获取玩家已接取任务数量(string 玩家名)
        {
            lock (_dbLock)
            {
                using var conn = 获取连接();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM 黑市任务 WHERE 玩家名 = @玩家名 AND 状态 IN (1, 2)";
                cmd.Parameters.AddWithValue("@玩家名", 玩家名);
                var result = cmd.ExecuteScalar();
                if (result == null || result == DBNull.Value) return 0;
                return Convert.ToInt32(result);
            }
        }

        public static 货币数据 加载() => new 货币数据();
        public void 保存() { }
    }
}
