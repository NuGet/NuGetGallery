using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;


namespace ObfuscateAuditLogs
{
    public struct LogData
    {
        public LogStatus LogStatus { get; }

        public string Message { get; }

        public string Run { get; }

        public string Operation { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public LogData(LogStatus logStatus, string run, string message)
        {
            LogStatus = logStatus;
            Run = run;
            Message = message;
            Operation = string.Empty;
            Timestamp = DateTimeOffset.UtcNow;
        }
    }

    public enum LogStatus
    {
        Fail,
        Info,
        Pass
    }

    public interface ILog
    {
        Task LogAsync(LogData data);
    }

    public class FileLog : ILog
    {
        string _path;

        public FileLog(string storageInfo)
        {
            _path = storageInfo;
        }

        public Task LogAsync(LogData data)
        {
            var currentTime = System.DateTime.UtcNow;
            var fileName = $"{data.Run}_{data.LogStatus}_{currentTime.Year}_{currentTime.Month}_{currentTime.Day}_{currentTime.Hour}_{currentTime.Minute}_{currentTime.Second}";
            File.WriteAllText(Path.Combine(_path, $"{fileName}.txt"), data.Message);
            return Task.FromResult(true);
        }
    }

    public class SQLLog : ILog
    {
        string _connectionString;
        const int RetryCount = 5;
        const string tableLogName = "packageaudittransformlog";

        FileLog _bakFileLog;

        public SQLLog(string connectionString)
        {
            _connectionString = connectionString;
            var bakLog = Path.Combine(Path.GetTempPath(), "0_NuGetAuditSQLBakLog");
            Directory.CreateDirectory(bakLog);
            _bakFileLog = new FileLog(bakLog);
        }

        public async Task LogAsync(LogData data)
        {
            int retries = 0;
            while (retries < RetryCount)
            {
                using (SqlConnection connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    try
                    {
                        string sqlcmd = $"INSERT INTO {tableLogName}" +
                                                        $"(Run,Timestamp,FileName,Status,Message)" +
                                                        $" VALUES('{data.Run}','{data.Timestamp}','{data.Operation}', '{data.LogStatus}', '{data.Message}')";

                        SqlCommand Cmd = new SqlCommand(sqlcmd,
                                                        connection);
                        int c = await Cmd.ExecuteNonQueryAsync();
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogData newdata = new LogData(LogStatus.Fail, data.Run,
                            $"SQLException:{ex} \nExecution:{data.Run}," +
                            $"{data.Timestamp},{data.Operation}," +
                            $"{data.LogStatus}, {data.Message}");

                        await _bakFileLog.LogAsync(newdata);
                    }
                    connection.Close();
                }
                retries++;
            }
        }
    }
}
