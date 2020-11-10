using Microsoft.Extensions.DependencyInjection;
using Sitecore.Abstractions;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.DependencyInjection;
using Sitecore.IO;
using Sitecore.Links;
using Sitecore.Sites;
using Sitecore.Text;
using Sitecore.Web;
using Sitecore.XA.Foundation.Abstractions;
using Sitecore.XA.Foundation.Multisite;
using Sitecore.XA.Foundation.SitecoreExtensions.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sitecore.Support.XA.Foundation.Multisite
{
    public class SiteInfoResolver : ISiteInfoResolver
    {
        private IEnumerable<string> _sitePaths;

        private IEnumerable<SiteInfo> _sites;

        public virtual IEnumerable<SiteInfo> Sites
        {
            get
            {
                BaseSiteContextFactory service = ServiceLocator.ServiceProvider.GetService<BaseSiteContextFactory>();
                return _sites ?? (_sites = from s in service.GetSites()
                                           where s.EnablePreview
                                           orderby s.RootPath descending
                                           select s);
            }
        }

        public virtual IEnumerable<string> SitePaths => _sitePaths ?? (_sitePaths = from s in Sites.Select(GetStartPath)
                                                                                    select s.ToLower());

        protected IContext Context
        {
            get;
        } = ServiceLocator.ServiceProvider.GetService<IContext>();


        public virtual SiteInfo GetSiteInfo(Item item)
        {
            if (item != null)
            {
                SiteInfo[] array = DiscoverPossibleSites(item);
                if (array.Length <= 1)
                {
                    return array.FirstOrDefault();
                }
                if (HttpContext.Current != null)
                {
                    SiteInfo siteInfo = ResolveSiteFromQuery(array, HttpContext.Current.Request);
                    if (siteInfo != null)
                    {
                        return siteInfo;
                    }
                }
                SiteInfo siteInfo2 = array.FirstOrDefault(delegate (SiteInfo s)
                {
                    if (Context.Site != null)
                    {
                        return s.Name == Context.Site.Name;
                    }
                    return false;
                });
                if (siteInfo2 != null)
                {
                    return siteInfo2;
                }
                if (HttpContext.Current != null)
                {
                    SiteInfo siteInfo3 = ResolveSiteFromRequest(array, new HttpRequestWrapper(HttpContext.Current.Request));
                    if (siteInfo3 != null)
                    {
                        return siteInfo3;
                    }
                }
                return array.FirstOrDefault((SiteInfo s) => LanguagesMatch(s, item)) ?? array.FirstOrDefault();
            }
            return null;
        }

        public virtual SiteInfo ResolveSiteFromRequest(SiteInfo[] possibleSites, HttpRequestBase request)
        {
            return ResolveSiteFromRequestImpl(possibleSites, request.Url.Host, request.Path);
        }

        public virtual string GetHomeUrl(Item item)
        {
            SiteInfo siteInfo = GetSiteInfo(item);
            string startPath = GetStartPath(siteInfo);
            Item item2 = ServiceLocator.ServiceProvider.GetService<IContentRepository>().GetItem(startPath);
            UrlOptions defaultOptions = UrlOptions.DefaultOptions;
            defaultOptions.Site = SiteContextFactory.GetSiteContext(siteInfo.Name);
            return new UrlString((item2 != null) ? LinkManager.GetItemUrl(item2, defaultOptions) : string.Empty).Path;
        }

        public virtual void Reset()
        {
            _sites = null;
            _sitePaths = null;
        }

        public virtual string GetRootPath(Item item)
        {
            return GetRootPath(GetSiteInfo(item));
        }

        public virtual string GetRootPath(SiteInfo site)
        {
            if (site == null)
            {
                return string.Empty;
            }
            return site.RootPath;
        }

        public virtual string GetStartPath(Item item)
        {
            return GetStartPath(GetSiteInfo(item));
        }

        public virtual string GetStartPath(SiteInfo site)
        {
            if (site == null)
            {
                return string.Empty;
            }
            return FileUtil.MakePath(site.RootPath, site.StartItem);
        }

        public virtual string GetRedirectUrl(ID currentItemId, ID targetSiteId)
        {
            IContentRepository service = ServiceLocator.ServiceProvider.GetService<IContentRepository>();
            Item item = service.GetItem(currentItemId);
            Item item2 = service.GetItem(GetStartPath(item));
            Item item3 = service.GetItem(targetSiteId);
            Item item4 = service.GetItem(GetStartPath(item3));
            string path = item.Paths.Path.Replace(item2.Paths.Path, item4.Paths.Path);
            Item item5 = service.GetItem(path) ?? item4;
            SiteInfo siteInfo = GetSiteInfo(item5);
            SiteInfo siteInfo2 = GetSiteInfo(item);
            List<string> source = siteInfo.HostName.Split('|').ToList();
            string itemUrl = LinkManager.GetItemUrl(item5, new UrlOptions
            {
                Site = new SiteContext(siteInfo),
                LanguageEmbedding = LanguageEmbedding.Never
            });
            if (siteInfo2 != null && siteInfo2.VirtualFolder != siteInfo.VirtualFolder && siteInfo2.HostName.Split('|').FirstOrDefault() == source.FirstOrDefault())
            {
                return itemUrl;
            }
            string text = source.FirstOrDefault();
            string query = string.Empty;
            if (string.IsNullOrEmpty(text) || text == "*")
            {
                text = HttpContext.Current.Request.Url.Host;
                query = $"sc_site={siteInfo.Name}";
            }
            return new UriBuilder
            {
                Host = text,
                Path = itemUrl,
                Scheme = HttpContext.Current.Request.Url.Scheme,
                Query = query
            }.ToString();
        }

        protected virtual SiteInfo[] DiscoverPossibleSites(Item item)
        {
            return (from siteInfo in Sites
                    where IsMatchingPath(item, siteInfo)
                    select new KeyValuePair<int, SiteInfo>(siteInfo.RootPath.Length, siteInfo) into r
                    orderby r.Key descending
                    select r.Value).ToArray();
        }

        protected virtual bool IsMatchingPath(Item item, SiteInfo siteInfo)
        {
            string text = FormattableString.Invariant($"{item.Paths.FullPath}/");
            string value = FormattableString.Invariant($"{siteInfo.RootPath}/");
            return text.StartsWith(value, StringComparison.InvariantCultureIgnoreCase);
        }

        protected virtual SiteInfo ResolveByVirtualFolder(SiteInfo[] possibleSites, string requestPath)
        {
            return possibleSites.FirstOrDefault((SiteInfo s) => requestPath.StartsWith(s.VirtualFolder, StringComparison.OrdinalIgnoreCase));
        }

        public virtual SiteInfo ResolveSiteFromQuery(SiteInfo[] possibleSites, HttpRequest request)
        {
            SiteInfo result = null;
            string siteNameFromQuery = request.QueryString.Get("sc_site");
            if (!string.IsNullOrEmpty(siteNameFromQuery))
            {
                result = possibleSites.FirstOrDefault((SiteInfo s) => s.Name == siteNameFromQuery);
            }
            return result;
        }

        protected virtual bool LanguagesMatch(SiteInfo site, Item item)
        {
            if (item.Language.Name != site.Language)
            {
                return string.IsNullOrWhiteSpace(site.Language);
            }
            return true;
        }

        private SiteInfo ResolveSiteFromRequestImpl(SiteInfo[] possibleSites, string hostName, string requestPath)
        {
            SiteInfo[] array = possibleSites.Where(delegate (SiteInfo s)
            {
                if (s.TargetHostName != hostName)
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
                SiteInfo siteInfo = ResolveByVirtualFolder(array, requestPath);
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