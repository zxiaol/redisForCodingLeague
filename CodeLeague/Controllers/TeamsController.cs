using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using CodeLeague.Service;
using Newtonsoft.Json;

namespace CodeLeague.Controllers
{
    public class TeamsController : ApiController
    {
        public IHttpActionResult GetTeams()
        {
            var teamRankService = TeamRankService.Instance;

            var teams = teamRankService.GetTeamsByPassrate();
            
            //return Ok(JsonConvert.SerializeObject(teams));

            return Ok(teams);
        }
        
    }
}
