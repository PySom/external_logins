using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using ExternalAuth.Controllers.Models;
using ExternalAuth.Controllers.Models.ViewModel;
using ExternalAuth.Controllers.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using static Google.Apis.Auth.GoogleJsonWebSignature;

namespace ExternalAuth.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ExternalAPIsController : ControllerBase
    {
        private List<ExternalLogin> _externalLogins = new List<ExternalLogin>{
          new ExternalLogin { Id = "874757567", Provider = "google", UserId = 1 }
        };
        private List<User> _users = new List<User> {
            new User { Email = "Chisom Nwisu", FirstName = "Chisom", SurName = "Nwisu", Id = 1, 
                ExternalLogins = {  new ExternalLogin { Id = "874757567", Provider = "google", UserId = 1 } } }
        };

        //for facebook even for google, you get the users details from the client side.
        //If you dont't trust the client and you should not, then the logic below applies. Otherwise,
        //you can just request the data from the client and choose not to validate on the server, here.

        //TODO implement google login
        [HttpPost("auth/google")]
        [ProducesDefaultResponseType]
        public async Task<IActionResult> GoogleLogin(string token)
        {
            Payload payload = null;
            try
            {
                //If you want to enforce client ID use this
                //payload = await ValidateAsync(token, new ValidationSettings
                //{
                //    Audience = new[] { "646454628737-787367t96r78y8r7yweyf7er8.apps.googleusercontent.com" }
                //});
                //otherwise use this
                payload = await ValidateAsync(token);

                // It is important to add your ClientId as an audience in order to make sure
                // that the token is for your application!
            }
            catch (Exception ex)
            {
                Console.WriteLine($"message: {ex.Message}");
                // Invalid token
            }

            bool success = await ExternalLoginAsync("google", payload.Subject, payload.Email,
                payload.GivenName, payload.FamilyName, payload.Picture);
            if (success)
            {
                // perform extra logic
                return NoContent();
            }
            return BadRequest(new { Message = "Error message" });
        }


        //TODO Implement facebook login
        [HttpPost("auth/facebook")]
        [ProducesDefaultResponseType]
        public async Task<IActionResult> FacebookLogin(string token)
        {
            FacebookProfile profile = await NetworkHelper.GetFacebookAsync(token);
            if (profile != null)
            {
                string provider = "facebook";
                //please handle the logic to split names into first and last
                bool success = await ExternalLoginAsync(provider, profile.Id, profile.Email, profile.Name, "", profile.Picture?.Data?.Url);
                if (success)
                {
                    // perform extra logic
                    return NoContent();
                }
                return BadRequest(new { Message = "Error message" });
            }
            return BadRequest(new { Message = "We could not validate your facebook profile" });
        }

        private async ValueTask<bool> ExternalLoginAsync(string provider, string providerKey, string email, string firstName, string surName, string picture)
        {
            //Here you want to check if the provider exists 
            //first check if the user key and provider already exist in the db;
            bool exists = _externalLogins.Any(x => x.Id == providerKey && x.Provider == provider);
            if (exists)
            {
                //log the person in with your logic for login
                return await Task.Run(() => true);
            }
            else
            {
                //check if this user exists by getting the id since we may use this Id later
                int userId = _users.Where(u => !string.IsNullOrEmpty(email) && u.Email.ToLower() == email.ToLower()).Select(u => u.Id).FirstOrDefault();
                //0 means no id was found
                if(userId == 0)
                {
                    //we create this user and reassign userId
                    User user = new User { Email = email, FirstName = firstName, SurName = surName, Image = picture, Id = 2 }; //normally the db should increment id
                    _users.Add(user);
                    userId = user.Id;
                }
                _externalLogins.Add(new ExternalLogin { Provider = provider, Id = providerKey, UserId = userId });
                //log this person in with your login function
                return await Task.Run(() => true);
            }
        }
        
        
    }

    namespace Services
    {
        public static class NetworkHelper
        {
            public static async Task<FacebookProfile> GetFacebookAsync(string token)
            {
                //normally this is the somewhat basic form to do a get request;
                //Looking more into http client, you can do a lot more;
                //for instance, I can attach an httprequest to the client as thus

                //HttpRequestMessage requestMessage = new HttpRequestMessage
                //{
                //    Method = HttpMethod.Post
                    
                //}
                
                using HttpClient client = new HttpClient();
                //you can also add to the header by choosing the default headers available in a standard http request using
                //client.DefaultRequestHeaders.Authorization or whatever you want
                //or adding a custom 
                //client.DefaultRequestHeaders.Add("name", "value");
                string uri = $"https://graph.facebook.com/v2.12/me?fields=name,first_name,last_name,email,picture,id,graphDomain&access_token={token}";
                var response = await client.GetStringAsync(uri);
                FacebookProfile profile = null;
                if (!string.IsNullOrEmpty(response))
                {
                    try
                    {
                        profile = JsonSerializer.Deserialize<FacebookProfile>(response);
                    }
                    catch(Exception ex)
                    {
                        Debug.WriteLine($"Something bad happened: {ex.Message}");
                    }
                }
                return profile;
            }
        }
    }

    namespace Models
    {
        public class User
        {
            public int Id { get; set; }
            public string Email { get; set; }
            public string FirstName { get; set; }
            public string SurName { get; set; }
            public string Image { get; set; }
            public ICollection<ExternalLogin> ExternalLogins { get; set; }
        }

        public class ExternalLogin
        {
            [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
            public string Id { get; set; }
            public string Provider { get; set; }
            [ForeignKey("UserId")]
            public User User { get; set; }
            public int UserId { get; set; }
        }

        namespace ViewModel
        {
            //I am not exactly sure how the result from facebook's graph response comes
            //you may want to investigate it first;
            public class FacebookProfile
            {
                public string Name { get; set; }
                public string Email { get; set; }
                public string GraphDomain { get; set; }
                public FacebookPicture Picture { get; set; }
                public string Id { get; set; }
            }

            public class FacebookPicture
            {
                public FacebookPictureData Data { get; set; }
            }

            public class FacebookPictureData
            {
                public string Url { get; set; }
            }
        }
    }
}
