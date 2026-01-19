namespace WebFileBrowser.Configuration;

public class UserCredentials
{
    private readonly IDictionary<string, string> _shares;

    public UserCredentials(IConfiguration configuration)
    {
        var userCredentials = configuration.GetRequiredSection("Users").Get<IDictionary<string, string>>();
        _shares = new Dictionary<string, string>();
        foreach(var c in userCredentials)
        {
            _shares.Add(c.Key, c.Value);
        }
    }

    public bool Contains(string username)
    {
        return _shares.ContainsKey(username);
    }

    public string GetPassword(string username)
    {
        return _shares[username];
    }
}

class UserCredentialsSection
{
    public IDictionary<string, string> Values {get; set;}
}