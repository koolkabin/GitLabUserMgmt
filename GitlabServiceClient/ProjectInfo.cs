using Newtonsoft.Json;

namespace Utilities;
public record struct ProjectInfo(int Id,
string Name,
string path,
[property: JsonProperty("active_branch")] string activeBranch,
[property: JsonProperty("repo_url")] string repoUrl,
[property: JsonProperty("created_at")] DateTime createdDate);


