namespace WebFileBrowser.Configuration;

public class ShareMapping
{
    private readonly IDictionary<string, string> _shares;

    public ShareMapping(IConfiguration configuration)
    {
        var shares = configuration.GetRequiredSection("Shares").Get<IDictionary<string, string>>();
        _shares = new Dictionary<string, string>();
        foreach(var p in shares)
        {
            _shares.Add(p.Key, p.Value);
        }
    }

    public bool Contains(string shareName)
    {
        return _shares.ContainsKey(shareName);
    }

    public string GetSharePath(string shareName)
    {
        return _shares[shareName];
    }

    public IEnumerable<string> GetShares()
    {
        return _shares.Keys;
    }
}

class SharesConfigurationSection
{
    public IDictionary<string, string> Values {get; set;}
}