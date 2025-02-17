namespace GitLabUserMgmt
{
    public record struct ProjectInfo(int id,
        string name, 
        string path, 
        string activeBranch, 
        string repoUrl, 
        DateTime createdDate);
}
