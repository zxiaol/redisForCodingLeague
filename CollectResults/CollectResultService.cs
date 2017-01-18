using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using StackExchange.Redis;

namespace CollectResults
{
    public class TeamResult : IComparable<TeamResult>
    {
        public float PassRate { get; set; }
        public string FailTest { get; set; }
        public string Name { get; set; }

        public string UpdateTime { get; set; }

        public int? ExecutionTime { get; set; }

        public string Change { get; set; }

        public int Rank { get; set; }

        public TeamResult()
        {
            PassRate = 0;
            FailTest = "";
            ExecutionTime = null;
            Change = "---";
            UpdateTime = DateTime.Now.ToString();
        }

        public int CompareTo(TeamResult other)
        {
            if (other.PassRate.CompareTo(PassRate) == 0)
            {
                if (ExecutionTime == null && other.ExecutionTime == null)
                {
                    return 0;
                }

                if (ExecutionTime == null)
                {
                    return 1;

                }
                
                if (other.ExecutionTime == null)
                {
                    return -1;
                }

                return ExecutionTime.Value.CompareTo(other.ExecutionTime.Value);

            }

            return other.PassRate.CompareTo(PassRate);
        }
    }
    public partial class CollectResultService : ServiceBase
    {
        private const string LogSource = "BGCCodingLeagueResultCollect";
        private const string LogName = "CollectLog";
        private System.Timers.Timer timer;
        private string exeDir;
        private Dictionary<string, TeamResult> results = new Dictionary<string, TeamResult>();
        private ConnectionMultiplexer redis = null;

        public CollectResultService()
        {
            InitializeComponent();
            eventLog = new EventLog();
            if (!EventLog.SourceExists(LogSource))
            {
                EventLog.CreateEventSource(
                    LogSource, LogName);
            }
            eventLog.Source = LogSource;
            eventLog.Log = LogName;
            exeDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        protected override void OnStart(string[] args)
        {
            eventLog.WriteEntry("In OnStart");
            timer = new System.Timers.Timer();
            timer.Interval = Int32.Parse(ConfigurationManager.AppSettings["ScanInterval"]);
            timer.Elapsed += OnTimer;
            timer.Start();
        }

        private void OnTimer(object sender, System.Timers.ElapsedEventArgs e)
        {

            eventLog.WriteEntry("Collecting result", EventLogEntryType.Information);

            try
            {
                results.Clear();
                CopyResultsToLocal();
                ParseResults();
                SaveResultsToMemory();

            }
            catch (Exception exception)
            {
                eventLog.WriteEntry("Collecting result failed.Details:" + exception.Message, EventLogEntryType.Error);
            }

            eventLog.WriteEntry("Collecting result finished", EventLogEntryType.Information);

        }

        private void SaveResultsToMemory()
        {
            CreateRedisIfNotExist();

            var db = GetRedisDB();

            var sortedResults = results.Values.ToList();
            sortedResults.Sort();
            PopulateRank(sortedResults);
            PopulateOrderChange(db, sortedResults);
            var sortedTeamNames = "";
            foreach (var result in sortedResults)
            {
                sortedTeamNames += (result.Name + ",");
                db.HashSet(result.Name,ConvertToHashEntryList(result).ToArray());
            }
            db.StringSet("SortedTeamNames", sortedTeamNames);
        }

        private void PopulateOrderChange(IDatabase db, List<TeamResult> sortedResults)
        {
           bool isOrderChanged = false;

            foreach (var result in sortedResults)
            {
                if (db.KeyExists(result.Name))
                {
                    if (db.HashExists(result.Name, "Rank"))
                    {
                        var lastRank = Int32.Parse(db.HashGet(result.Name, "Rank").ToString());
                        if (lastRank < result.Rank)
                        {
                            result.Change = String.Format("-{0}", result.Rank - lastRank);
                            isOrderChanged = true;
                        }
                        else if (lastRank > result.Rank)
                        {
                            result.Change = String.Format("+{0}", lastRank - result.Rank);
                            isOrderChanged = true;
                        }
                        
                    }
                    
                }
            }

            if (isOrderChanged)
            {
                foreach (var result in sortedResults)
                {
                    if (db.KeyExists(result.Name))
                    {
                        if (db.HashExists(result.Name, "Rank"))
                        {
                            var lastRank = Int32.Parse(db.HashGet(result.Name, "Rank").ToString());
                            if (lastRank == result.Rank)
                            {
                                result.Change = "---";
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var result in sortedResults)
                {
                    if (db.KeyExists(result.Name))
                    {
                        if (db.HashExists(result.Name, "Change"))
                        {
                            result.Change = db.HashGet(result.Name, "Change").ToString();
                        }
                    }
                }
            }
        }

        private void PopulateRank(List<TeamResult> sortedResults)
        {

            for (var i = 0; i < sortedResults.Count; i++)
            {

                if (i > 0 && sortedResults[i].CompareTo(sortedResults[i - 1]) == 0)
                {
                    sortedResults[i].Rank = sortedResults[i - 1].Rank;
                }
                else
                {
                    sortedResults[i].Rank = i + 1;
                }
            }
        }

        private IDatabase GetRedisDB()
        {
            try
            {
                return redis.GetDatabase();

            }
            catch (Exception)
            {
                redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisConnectString"]);

            }
            return redis.GetDatabase();
        }

        private List<HashEntry> ConvertToHashEntryList(TeamResult result)
        {
            var propertiesInHashEntryList = new List<HashEntry>();


            propertiesInHashEntryList.Add(new HashEntry("Name", result.Name));

            propertiesInHashEntryList.Add(new HashEntry("PassRate", result.PassRate.ToString()));
            propertiesInHashEntryList.Add(new HashEntry("FailTest", result.FailTest));
            propertiesInHashEntryList.Add(new HashEntry("UpdateTime", result.UpdateTime));
            var executionTime = "NA";
            if (result.ExecutionTime != null)
            {
                executionTime = string.Format("{0} {1}", result.ExecutionTime,
                    ConfigurationManager.AppSettings["TimeDurationUnit"]);
            }
            propertiesInHashEntryList.Add(new HashEntry("ExecutionTime", executionTime));

            propertiesInHashEntryList.Add(new HashEntry("Rank", result.Rank.ToString()));

            propertiesInHashEntryList.Add(new HashEntry("Change", result.Change));

            return propertiesInHashEntryList;
        }

        private void CreateRedisIfNotExist()
        {
            if (redis == null)
            {
                redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisConnectString"]);
            }
        }

        private void ParseResults()
        {
            var resultFileMatchString = ConfigurationManager.AppSettings["ResultFileMatchString"];
            var files = Directory.GetFiles(exeDir, resultFileMatchString);
            foreach (var file in files)
            {
                ParseFileToDic(file);
            }
        }

        private void ParseFileToDic(string file)
        {
            var strTimeDurationUnit = ConfigurationManager.AppSettings["TimeDurationUnit"].ToLower();
            try
            {
                var lines = File.ReadLines(file);
                var result = new TeamResult();
                foreach (var line in lines)
                {
                    var lineValue = line.Trim();
                    if (String.IsNullOrWhiteSpace(lineValue) || String.IsNullOrEmpty(lineValue))
                        continue;

                    var separator = lineValue.IndexOf(":");

                    if (separator >= lineValue.Length - 1)
                        continue;

                    var key = lineValue.Substring(0, separator);
                    var value = lineValue.Substring(separator + 1);

                    if (key.ToLower().Contains("name"))
                    {
                        result.Name = value;
                    }
                    else if (key.ToLower().Contains("passrate"))
                    {
                        float pass = 0.0f;
                        if (float.TryParse(value, out pass))
                        {
                            result.PassRate = pass;
                        }
                    }
                    else if (key.ToLower().Contains("failtest"))
                    {
                        result.FailTest = value;
                    }
                    else if (key.ToLower().Contains("updatetime"))
                    {

                        result.UpdateTime = value;

                    }
                    else if (key.ToLower().Contains("executiontime"))
                    {
                        if (value.ToLower().Contains(strTimeDurationUnit))
                        {
                            value = value.ToLower().Replace(strTimeDurationUnit, "");
                        }

                        int time = 0;
                        if (Int32.TryParse(value, out time))
                        {
                            result.ExecutionTime = time;
                        }

                    }
                }

                if (!string.IsNullOrEmpty(result.Name))
                {
                    results[result.Name] = result;
                }

            }
            catch (Exception e)
            {

                eventLog.WriteEntry(
                    String.Format("Parse result from {0} failed. Details:{1}", file,
                        e.Message), EventLogEntryType.Error);
            }
            

        }

        private void CopyResultsToLocal()
        {
            RemoveOldResultsFile();
            var srcResultRoot = ConfigurationManager.AppSettings["ResultRootFolder"];
            var resultFileMatchString = ConfigurationManager.AppSettings["ResultFileMatchString"];

            try
            {
                var files = Directory.GetFiles(srcResultRoot, resultFileMatchString,SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        eventLog.WriteEntry("Copy file" + file + " to " + exeDir, EventLogEntryType.Information);

                        try
                        {
                            File.Copy(file, Path.Combine(exeDir, Path.GetFileName(file)), true);
                        }
                        catch (Exception e)
                        {
                            eventLog.WriteEntry(
                                String.Format("Failed to Copy file {0} to {1}. details:{2}", file, exeDir, e.Message),
                                EventLogEntryType.Error);
                        }
                    }
                }
            }
            catch (Exception e)
            {

                eventLog.WriteEntry(
                    String.Format("Failed to find files under {0} match {1}. Details:{2}", srcResultRoot,
                        resultFileMatchString, e.Message), EventLogEntryType.Error);

            }
        }

        private void RemoveOldResultsFile()
        {
            var resultFileMatchString = ConfigurationManager.AppSettings["ResultFileMatchString"];
            var files = Directory.GetFiles(exeDir, resultFileMatchString);
            foreach (var file in files)
            {
                File.Delete(file);
            }
        }

        protected override void OnStop()
        {
            timer.Stop();
            eventLog.WriteEntry("In onStop.");
        }
    }
}
