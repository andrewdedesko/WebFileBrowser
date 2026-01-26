using System.Text.RegularExpressions;

namespace WebFileBrowser.Configuration;

public class DefaultViews
{
    public IEnumerable<Regex> ThumbnailViewPathPatterns {get; private set;}

    public DefaultViews(IConfiguration configuration)
    {
        var patternStrings = configuration.GetSection("ThumbnailViewPatterns").Get<List<string>>();
        if(patternStrings == null)
        {
            ThumbnailViewPathPatterns = Enumerable.Empty<Regex>();
        }else{
            ThumbnailViewPathPatterns = patternStrings.Select(p => new Regex(p, RegexOptions.IgnoreCase))
            .AsEnumerable();
        }
    }
}