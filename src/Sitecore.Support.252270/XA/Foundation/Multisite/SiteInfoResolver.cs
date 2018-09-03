namespace Sitecore.Support.XA.Foundation.Multisite
{
  using Sitecore.Web;
  using System;
  using System.Linq;
  using System.Web;

  public class SiteInfoResolver : Sitecore.XA.Foundation.Multisite.SiteInfoResolver
  {
    [Obsolete("This method will be removed in SXA 1.7. Use ResolveSiteFromRequest(SiteInfo[] possibleSites, HttpRequestBase request) instead.")]
    public override SiteInfo ResolveSiteFromRequest(SiteInfo[] possibleSites, HttpRequest request)
    {
      return ResolveSiteFromRequestImpl(possibleSites, request.Url.Host, request.Path);
    }

    public override SiteInfo ResolveSiteFromRequest(SiteInfo[] possibleSites, HttpRequestBase request)
    {
      return ResolveSiteFromRequestImpl(possibleSites, request.Url.Host, request.Path);
    }
    
    private SiteInfo ResolveSiteFromRequestImpl(SiteInfo[] possibleSites, string hostName, string requestPath)
    {
      SiteInfo[] array = possibleSites.Where(delegate (SiteInfo s)
      {
        if (!(s.TargetHostName == hostName))
        {
          return s.HostName.Split('|').Contains(hostName);
        }
        return true;
      }).ToArray();
      if (array.Length == 1)
      {
        return array.First();
      }
      if (array.Length > 1)
      {
        SiteInfo siteInfo = ResolveByVirtualFolder(possibleSites, requestPath);
        if (siteInfo != null)
        {
          return siteInfo;
        }
      }
      SiteInfo[] array2 = (from s in possibleSites
                           where s.HostName.Split('|').Contains("*")
                           select s).ToArray();
      if (array2.Length == 1)
      {
        return array2.First();
      }
      if (array2.Length > 1)
      {
        SiteInfo siteInfo2 = ResolveByVirtualFolder(array2, requestPath);
        if (siteInfo2 != null)
        {
          return siteInfo2;
        }
      }
      return null;
    }
  }
}