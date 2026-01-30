using IISLogAnalyzer_WPF.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IISLogAnalyzer_WPF.Services
{
    public class LogParser
    {
        public async Task<List<LogEntry>> ParseLogFileAsync(string filePath)
        {
            var logs = new List<LogEntry>();
            var fieldMap = new Dictionary<string, int>();
            bool mapCreated = false;

            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    if (line.StartsWith("#"))
                    {
                        if (line.StartsWith("#Fields:"))
                        {
                            CreateFieldMap(line, fieldMap);
                            mapCreated = true;
                        }
                        continue;
                    }

                    if (!mapCreated) continue; // Cannot parse without map

                    try
                    {
                        var entry = ParseLine(line, fieldMap);
                        if (entry != null)
                        {
                            logs.Add(entry);
                        }
                    }
                    catch
                    {
                        // Skip malformed lines or log error if needed
                    }
                }
            }

            return logs;
        }

        private void CreateFieldMap(string line, Dictionary<string, int> fieldMap)
        {
            fieldMap.Clear();
            var fields = line.Substring(8).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < fields.Length; i++)
            {
                fieldMap[fields[i]] = i;
            }
        }

        private LogEntry? ParseLine(string line, Dictionary<string, int> fieldMap)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.None); // Don't remove empty entries, columns might be empty
            
            // Basic validation
            if (parts.Length < fieldMap.Count) return null;

            var entry = new LogEntry();

            // Date & Time
            string dateStr = GetValue(parts, fieldMap, "date");
            string timeStr = GetValue(parts, fieldMap, "time");
            if (!string.IsNullOrEmpty(dateStr) && !string.IsNullOrEmpty(timeStr))
            {
                if (DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    entry.Timestamp = dt;
                }
            }
            
             // Common Fields
            entry.Method = GetValue(parts, fieldMap, "cs-method");
            entry.UriStem = GetValue(parts, fieldMap, "cs-uri-stem");
            entry.UriQuery = GetValue(parts, fieldMap, "cs-uri-query");
            entry.Username = GetValue(parts, fieldMap, "cs-username");
            entry.ClientIp = GetValue(parts, fieldMap, "c-ip");
            entry.UserAgent = GetValue(parts, fieldMap, "cs(User-Agent)").Replace("+", " "); // Decode spaces often represented as + in logs

            // Integers
            if (int.TryParse(GetValue(parts, fieldMap, "s-port"), out int port)) entry.Port = port;
            if (int.TryParse(GetValue(parts, fieldMap, "sc-status"), out int status)) entry.StatusCode = status;
            if (int.TryParse(GetValue(parts, fieldMap, "sc-substatus"), out int subStatus)) entry.SubStatus = subStatus;
            if (int.TryParse(GetValue(parts, fieldMap, "time-taken"), out int timeTaken)) entry.TimeTaken = timeTaken;
            if (long.TryParse(GetValue(parts, fieldMap, "sc-bytes"), out long bytesSent)) entry.BytesSent = bytesSent;

            return entry;
        }

        private string GetValue(string[] parts, Dictionary<string, int> map, string fieldName)
        {
            return map.TryGetValue(fieldName, out int index) && index < parts.Length ? parts[index] : string.Empty;
        }
    }
}
