using Refit;
using Utilities;

namespace GitlabServiceClient;

public interface IGitLabApi
{
    [Get("/user")]
    Task<GitLabUser> GetAuthenticatedUserAsync([Header("Authorization")] string authorization);
    [Get("/users/?username={username}")]
    Task<List<GitLabUser>> GetUserByUserNameAsync(string username);

    [Get("/projects?owned=true&page={page}&per_page={perPage}")]
    Task<List<ProjectInfo>> GetOwnedProjectsAsync(
        int page,
        int perPage,
        [Header("Authorization")] string authorization
    );

    [Get("/projects/{id}")]
    Task<ProjectInfo> GetProjectByIdAsync(int id, [Header("Authorization")] string authorization);

    [Delete("/projects/{projectId}/members/{userId}")]
    Task RemoveUserFromProjectAsync(int projectId, int userId, [Header("Authorization")] string authorization);

}
