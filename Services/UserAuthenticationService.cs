using WebFileBrowser.Configuration;

namespace WebFileBrowser.Services;

class UserAuthenticationService : IUserAuthenticationService
{
    private readonly UserCredentials _userCredentials;

    public UserAuthenticationService(UserCredentials userCredentials)
    {
        _userCredentials = userCredentials;
    }

    public bool Authenticate(string username, string password)
    {
        if (!_userCredentials.Contains(username))
        {
            return false;
        }

        if(_userCredentials.GetPassword(username) == password)
        {
            return true;
        }

        return false;
    }
}