using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Configuration;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GitLabUserMgmt
{
    public partial class MainWindow : Window
    {
        private string GITLAB_TOKEN;
        private string GITLAB_HOST;
        private string OWNER_USERNAME;
        private readonly HttpClient client = new HttpClient();

        public MainWindow()
        {
            InitializeComponent();
            GITLAB_HOST = "https://gitlab.com";
            //(GITLAB_TOKEN, OWNER_USERNAME, GITLAB_HOST) = LoadConfiguration();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GITLAB_TOKEN);
        }

        private async void RemoveUser_Click(object sender, RoutedEventArgs e)
        {
            string usernameToRemove = UsernameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(usernameToRemove))
            {
                MessageBox.Show("Please enter a username.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            OWNER_USERNAME = OwnerTextBox.Text.Trim();
            if (string.IsNullOrEmpty(OWNER_USERNAME))
            {
                MessageBox.Show("Please enter a owner user.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            GITLAB_TOKEN = TokenTextBox.Password.Trim();
            if (string.IsNullOrEmpty(usernameToRemove))
            {
                MessageBox.Show("Please enter a valid Personal Access Token.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SuccessListBox.Items.Clear();
            FailedListBox.Items.Clear();
            SuccessListBox.Items.Add($"Fetching user ID for '{usernameToRemove}'...");

            try
            {
                // Step 1: Get User ID
                int userIdToRemove = await GetUserId(usernameToRemove);
                if (userIdToRemove == -1) return;

                // Step 2: Get All Projects Owned by "koolkabin"
                var projects = await GetOwnedProjects();
                if (projects.Count == 0)
                {
                    FailedListBox.Items.Add($"❌ No projects found for owner '{OWNER_USERNAME}'.");
                    return;
                }

                // Step 3: Remove User from All Projects
                foreach (var projectId in projects)
                {
                    await RemoveUserFromProject(projectId, userIdToRemove, usernameToRemove);
                }

                SuccessListBox.Items.Add($"✅ Finished processing.");


            }
            catch (Exception ex)
            {
                FailedListBox.Items.Add($"❌ Error: {ex.Message}");
            }
        }
        private async Task<int> GetUserId(string username)
        {
            //Console.WriteLine($"DEBUG: Using Token - {GITLAB_TOKEN.Substring(0, 5)}********");

            string userApiUrl = $"{GITLAB_HOST}/api/v4/users?username={username}";
            var userResponse = await client.GetStringAsync(userApiUrl);
            var userData = JArray.Parse(userResponse);

            if (userData.Count == 0)
            {
                FailedListBox.Items.Add($"❌ User '{username}' not found.");
                return -1;
            }

            int userId = userData[0]["id"].Value<int>();
            SuccessListBox.Items.Add($"✅ Found User ID for '{username}': {userId}");
            return userId;
        }
        private async Task<List<ProjectInfo>> GetOwnedProjects()
        {
            List<ProjectInfo> projectIds = new List<ProjectInfo>();
            int page = 1;
            int perPage = 100; // Max allowed by GitLab

            while (true)
            {
                SuccessListBox.Items.Add($"🤞 Reading Paginated Project List : {page}");

                string projectsApiUrl = $"{GITLAB_HOST}/api/v4/projects?owned=true&simple=true&per_page={perPage}&page={page}";
                var response = await client.GetAsync(projectsApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    FailedListBox.Items.Add($"❌ Failed to fetch projects: {response.StatusCode}");
                    return projectIds;
                }
                var projectsData = JArray.Parse(await response.Content.ReadAsStringAsync());
                if (projectsData.Count == 0) break; // No more projects

                foreach (var project in projectsData)
                {
                    projectIds.Add(new ProjectInfo(project["id"].Value<int>(),
                        project["name"].Value<string>(),
                        project["path"].Value<string>(),
                        project["default_branch"].Value<string>(),
                        project["http_url_to_repo"].Value<string>(),
                        project["created_at"].Value<DateTime>()
                        ));
                }

                page++; // Move to next page
            }

            SuccessListBox.Items.Add($"✅ Found {projectIds.Count} projects owned by '{OWNER_USERNAME}'.");
            return projectIds;
        }

        private async Task RemoveUserFromProject(ProjectInfo projectInfo, int userId, string username)
        {
            string removeUserUrl = $"{GITLAB_HOST}/api/v4/projects/{projectInfo.id}/members/{userId}";
            var removeResponse = await client.DeleteAsync(removeUserUrl);

            if (removeResponse.IsSuccessStatusCode)
            {
                SuccessListBox.Items.Add($"✅ Removed '{username}' from project {projectInfo.name}");
            }
            else
            {
                FailedListBox.Items.Add($"❌ Failed to remove '{username}' from project {projectInfo.name} Reason: {removeResponse.ReasonPhrase}");
            }
        }
        private (string, string, string) LoadConfiguration()
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            environment = Environment.GetEnvironmentVariable("DOTNET_MODIFIABLE_ASSEMBLIES") ?? "debug";
            environment = environment == "debug" ? "Development" : "Production"; // "debug" is the default value for "DOTNET_MODIFIABLE_ASSEMBLIES

            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true) // Load environment-specific settings
                .Build();

            string token = config["GitLab:Token"];
            string owner = config["GitLab:owner"];
            string host = config["GitLab:Host"];

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(host))
            {
                MessageBox.Show($"GitLab Token or Host is missing in appsettings.{environment}.json", "Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }

            return (token, owner, host);
        }
    }
}
