namespace WebFileBrowser.Services;

public interface IUserAuthenticationService
{
    public bool Authenticate(string username, string password);
}