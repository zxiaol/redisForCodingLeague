using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace CodeLeague.Service
{
    public class TeamRankService
    {
        
        private static readonly Lazy<TeamRankService> lazy = new Lazy<TeamRankService>(() => new TeamRankService());

        private static ConnectionMultiplexer redis;

        private TeamRankService()
        {
            redis = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["RedisConnectString"]);
        }

        public static TeamRankService Instance
        {
            get { return lazy.Value; }
        }

        public TeamRankResults GetTeamsByPassrate()
        {

            return ReadResultsFromRedis();
        }

        private IDatabase GetRedisDb()
        {
            try
            {
                return redis.GetDatabase();
            }
            catch (Exception)
            {

            }

            return null;
        }

        private TeamRankResults ReadResultsFromRedis()
        {
            var rankTeamsDto = new TeamRankResults();

            var db = GetRedisDb();
            if (db == null)
            {
                return rankTeamsDto;
            }

            var sortedTeamNames = db.StringGet("SortedTeamNames").ToString();

            if (string.IsNullOrEmpty(sortedTeamNames))
            {
                return rankTeamsDto;
            }

            var teamNames = sortedTeamNames.Split(',');
            var teams = new List<TeamDto>();
            int deltChange = 0;
            string changedTeam = "";
            foreach (var teamName in teamNames)
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(teamName) || String.IsNullOrEmpty(teamName))
                        continue;

                    var team = new TeamDto();
                    team.Name = db.HashGet(teamName, "Name").ToString();
                    team.FailTest = db.HashGet(teamName, "FailTest").ToString();
                    var passrate = float.Parse(db.HashGet(teamName, "PassRate").ToString());
                    team.PassRate = (passrate*100).ToString("0.00");
                    team.UpdateTime = ConvertToTimeString(db.HashGet(teamName, "UpdateTime").ToString());
                    team.ExecutionTime = db.HashGet(teamName, "ExecutionTime").ToString();
                    team.Rank = Int32.Parse(db.HashGet(teamName, "Rank").ToString());
                    team.Change = db.HashGet(teamName, "Change").ToString();

                    var change = team.GetDeltChange();
                    if (change > deltChange)
                    {
                        deltChange = change;
                        changedTeam = team.Name;
                    }
                    teams.Add(team);

                }
                catch (Exception)
                {
                }
            }

            rankTeamsDto.Teams = teams;
            if (deltChange > 0)
            {
                rankTeamsDto.ChangeInfo = String.Format("Congratulations to {0} who has moved up by {1} positions.",
                    changedTeam, deltChange);
            }

            return rankTeamsDto;

        }

        private string ConvertToTimeString(string strDate)
        {
            try
            {
                var time = DateTime.Parse(strDate);
                return time.ToLongTimeString();
            }
            catch (Exception)
            {
                return strDate;
                
            }
        }
    }
}
