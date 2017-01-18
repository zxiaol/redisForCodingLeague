using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace CodeLeague.Service
{
    public class TeamDto
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { set; get; }

        [JsonProperty(PropertyName = "passRate")]
        public string PassRate { set; get; }

        [JsonProperty(PropertyName = "failTest")]
        public string FailTest { set; get; }

        [JsonProperty(PropertyName = "updateTime")]
        public string UpdateTime { get; set; }

        [JsonProperty(PropertyName = "executionTime")]
        public string ExecutionTime { get; set; }

        [JsonProperty(PropertyName = "rank")]
        public int Rank { get; set; }

        [JsonProperty(PropertyName = "change")]
        public string Change { get; set; }


        public int GetDeltChange()
        {
            try
            {
                if (Change.Contains("---"))
                    return 0;

                if (Change.StartsWith("-"))
                    return -1 * Int32.Parse(Change.Substring(1));

                if (Change.StartsWith("+"))
                    return Int32.Parse(Change.Substring(1));
            }
            catch (Exception)
            {
                
            }

            return 0;


        }
    }

    public class TeamRankResults
    {
        public TeamRankResults()
        {
            Teams = new List<TeamDto>();
            ChangeInfo = "";
        }

        [JsonProperty(PropertyName = "teams")]
        public List<TeamDto> Teams { get; set; }

        [JsonProperty(PropertyName = "change")]
        public string ChangeInfo { get; set; }

    }
}
