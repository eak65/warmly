using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Web;
using System.Text.RegularExpressions;
using System.Diagnostics;
[assembly: OwinStartup(typeof(Warmly.Startup))]

namespace Warmly
{
    public partial class Startup
    {        
        static string[] Scopes = { GmailService.Scope.GmailReadonly };
        static string ApplicationName = "Gmail API .NET Quickstart";
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            UserCredential credential;

            using (var stream =
                new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
            {
                string credPath = System.Environment.GetFolderPath(
                    System.Environment.SpecialFolder.Personal);
                credPath = Path.Combine(credPath, ".credentials/gmail-dotnet-quickstart.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                Console.WriteLine("Credential file saved to: " + credPath);
            }

            // Create Gmail API service.
            var service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });

            // Define parameters of request.
            UsersResource.LabelsResource.ListRequest request = service.Users.Labels.List("me");

            // List labels.
            IList<Label> labels = request.Execute().Labels;
            Console.WriteLine("Labels:");
            if (labels != null && labels.Count > 0)
            {
                foreach (var labelItem in labels)
                {
                    Console.WriteLine("{0}", labelItem.Name);
                }
            }
            else
            {
                Console.WriteLine("No labels found.");
            }
            // service.Users
            // request.
       //     Console.Read();
            /*
                GAlerts alert =new GAlerts("ethan@studytreeapp.com","qpal1z11");
                alert.create("test", "en", "happens", "all", "best", "feed");

                foreach(AlertItem item in alert.getList())
                {
                    
                }
            */
            IList<string> urls = new List<string>();

            ListThreadsResponse threads = service.Users.Threads.List("me").Execute();
            foreach (var thread in threads.Threads.ToList())
            {
                if(thread.Snippet.Contains("Google student success"))
                {
                    var threadDetails = service.Users.Threads.Get("me", thread.Id).Execute();
                    var msg = service.Users.Messages.Get("me", threadDetails.Messages[0].Id).Execute();
                 //   byte[] encbuff = Encoding.UTF8.GetBytes(msg.Payload.Parts[0].Body.Data);
                 //   string decodedString = HttpServerUtility.UrlTokenEncode(encbuff);
                //    byte[] decbuff = HttpServerUtility.UrlTokenDecode(msg.Payload.Parts[0].Body.Data);
                  //   decodedString = Encoding.UTF8.GetString(decbuff);
                     string s = msg.Payload.Parts[0].Body.Data;
                     string incoming = s
     .Replace('_', '/').Replace('-', '+');
                     switch (s.Length % 4)
                     {
                         case 2: incoming += "=="; break;
                         case 3: incoming += "="; break;
                     }
                     byte[] bytes = Convert.FromBase64String(incoming);
                     string originalText = Encoding.ASCII.GetString(bytes);
                    var matchCollection = Regex.Matches(originalText,"(http|ftp|https)://([\\w_-]+(?:(?:\\.[\\w_-]+)+))([\\w.,@?^=%&:/~+#-]*[\\w@?^=%&/~+#-])?");
                    foreach(Match m in matchCollection)
                    {
                        if (!m.Value.Contains("https://www.google.com/alerts"))
                        {
                            urls.Add(m.Value);
                        }
                    }
                }
            }

        }
    }
}
