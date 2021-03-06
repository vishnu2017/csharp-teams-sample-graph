﻿

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft_Teams_Graph_RESTAPIs_Connect.Models;
using Newtonsoft.Json;
using Resources;

namespace Microsoft_Teams_Graph_RESTAPIs_Connect.ImportantFiles
{
    public static class Statics
    {
        public static T Deserialize<T>(this string result)
        {
            return JsonConvert.DeserializeObject<T>(result);
        }
    }

    public class GraphService
    {
        /// <summary>
        /// Create new channel.
        /// </summary>
        /// <param name="accessToken">Access token to validate user</param>
        /// <param name="groupId">Id of the team in which new channel needs to be created</param>
        /// <param name="channelName">New channel name</param>
        /// <param name="channelDescription">New channel description</param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> CreateChannel(string accessToken, string groupId, string channelName, string channelDescription)
        {
            string endpoint = ServiceHelper.GraphRootUri + "groups/" + groupId + "/channels";

            Channel content = new Channel()
            {
                description = channelDescription,
                displayName = channelName
            };

            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Post, endpoint, accessToken, content);

            return response;//.ReasonPhrase;
        }

        /// <summary>
        /// Get all channels of the given.
        /// </summary>
        /// <param name="accessToken">Access token to validate user</param>
        /// <param name="groupId">Id of the team to get all associated channels</param>
        /// <returns></returns>
        public async Task<IEnumerable<ResultsItem>> GetChannels(string accessToken, string groupId, string resourcePropId)
        {
            string endpoint = ServiceHelper.GraphRootUri + "groups/" + groupId + "/channels";
            string idPropertyName = "id";
            string displayPropertyName = "displayName";

            List<ResultsItem> items = new List<ResultsItem>();
            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Get, endpoint, accessToken);
            if (response != null && response.IsSuccessStatusCode)
            {
                items = await ServiceHelper.GetResultsItem(response, idPropertyName, displayPropertyName, resourcePropId);

            }
            return items;
        }

        /// <summary>
        /// Get the current user's id from their profile.
        /// </summary>
        /// <param name="accessToken">Access token to validate user</param>
        /// <returns></returns>
        public async Task<string> GetMyId(String accessToken)
        {
            string endpoint = "https://graph.microsoft.com/v1.0/me";
            string queryParameter = "?$select=id";
            String userId = "";
            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Get, endpoint + queryParameter, accessToken);
            if (response != null && response.IsSuccessStatusCode)
            {
                var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                userId = json.GetValue("id").ToString();
            }
            return userId?.Trim();
        }

        /// <summary>
        /// Get all teams which user is the member of.
        /// </summary>
        /// <param name="accessToken">Access token to validate user</param>
        /// <returns></returns>
        public async Task<IEnumerable<ResultsItem>> GetMyTeams(string accessToken, string resourcePropId)
        {

            string endpoint = ServiceHelper.GraphRootUri + "me/joinedTeams";
            string idPropertyName = "id";
            string displayPropertyName = "displayName";

            List<ResultsItem> items = new List<ResultsItem>();
            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Get, endpoint, accessToken);
            if (response != null && response.IsSuccessStatusCode)
            {
                items = await ServiceHelper.GetResultsItem(response, idPropertyName, displayPropertyName, resourcePropId);

            }
            return items;
        }

        public async Task<HttpResponseMessage> PostMessage(string accessToken, string groupId, string channelId, string message)
        {
            string endpoint = ServiceHelper.GraphRootUri + "groups/" + groupId + "/channels/" + channelId + "/chatThreads";

            PostMessage content = new PostMessage()
            {
                rootMessage = new RootMessage()
                {
                    body = new Message()
                    {
                        content = message
                    }
                }
            };
            List<ResultsItem> items = new List<ResultsItem>();
            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Post, endpoint, accessToken, content);

            return response;//response.ReasonPhrase;
        }

        public async Task<string> CreateNewTeamAndGroup(string accessToken, Group group)
        {
            // create group
            string endpoint = ServiceHelper.GraphRootUri + "groups";
            if (group != null)
            {
                group.groupTypes = new string[] { "Unified" };
                group.mailEnabled = true;
                group.securityEnabled = false;
                group.visibility = "Private";
            }

            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Post, endpoint, accessToken, group);
            if (!response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            string responseBody = await response.Content.ReadAsStringAsync(); ;
            string groupId = responseBody.Deserialize<Group>().id;

            // add me as member
            string me = await GetMyId(accessToken);
            string payload = $"{{ '@odata.id': '{ServiceHelper.GraphRootUri}users/{me}' }}";
            HttpResponseMessage responseRef = await ServiceHelper.SendRequest(HttpMethod.Post,
                ServiceHelper.GraphRootUri + $"groups/{groupId}/members/$ref",
                accessToken, payload);

            // create team
            await AddTeamToGroup(groupId, accessToken);
            return $"Created {groupId}";
        }

        public async Task<String> AddTeamToGroup(string groupId, string accessToken)
        {
            string endpoint = ServiceHelper.GraphRootUri + "groups/" + groupId + "/team";
            Team team = new Models.Team();
            team.guestSettings = new Models.TeamGuestSettings() { allowCreateUpdateChannels = false, allowDeleteChannels = false };

            HttpResponseMessage response = await ServiceHelper.SendRequest(HttpMethod.Put, endpoint, accessToken, team);
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);
            return response.ReasonPhrase;
        }


        public async Task<String> UpdateTeam(string groupId, string accessToken)
        {
            string endpoint = ServiceHelper.GraphRootUri + "groups/" + groupId + "/team";
            Team team = new Models.Team();
            team.guestSettings = new Models.TeamGuestSettings() { allowCreateUpdateChannels = true, allowDeleteChannels = false };

            HttpResponseMessage response = await ServiceHelper.SendRequest(new HttpMethod("PATCH"), endpoint, accessToken, team);
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);
            return response.ReasonPhrase;
        }


        public async Task AddMember(string groupId, Member member, string accessToken)
        {
            string payload = $"{{ '@odata.id': '{ServiceHelper.GraphRootUri}users/{member.upn}' }}";
            string endpoint = ServiceHelper.GraphRootUri + $"groups/{groupId}/members/$ref";
            HttpResponseMessage responseRef = await ServiceHelper.SendRequest(HttpMethod.Post, endpoint, accessToken, payload);

            if (member.owner)
            {
                endpoint = ServiceHelper.GraphRootUri + $"groups/{groupId}/owners/$ref";
                HttpResponseMessage responseOwner = await ServiceHelper.SendRequest(HttpMethod.Post, endpoint, accessToken, payload);
            }

        }
    }
}