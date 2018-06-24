using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System;

class AlertItem
{
    public string id;
    public string type;
    public string term;
    public string url;
}

class GAlertException : Exception
{
    public GAlertException()    {    }
    public GAlertException(string message) : base(message)    {    }
}

class GAlerts
{
    protected string user;
    protected string pass;
    protected CookieContainer cookie;
    protected int timeout;
    protected bool logged;

    private int maxRedirs = 5;
    private string userAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1)";

    private string urlLogin = "https://accounts.google.com/ServiceLogin?hl=en&service=alerts&continue=http://www.google.com/alerts/manage";
    private string urlAuth = "https://accounts.google.com/ServiceLoginAuth";
    private string urlAlerts = "http://www.google.com/alerts";
    private string urlCreate = "http://www.google.com/alerts/create";
    private string urlManage = "http://www.google.com/alerts/manage";
    private string urlDelete = "http://www.google.com/alerts/save";


    /// <summary>
    /// GAlerts - Manage google alerts from a google account
    /// </summary>
    /// <param name="user">google account user name (email)</param>
    /// <param name="pass">google account password</param>
    public GAlerts(string user, string pass)
    {
        this.user = user;
        this.pass = pass;
        this.cookie = new CookieContainer();
        this.timeout = 30;
        this.logged = false;
    }

    /// <summary>
    /// GAlerts - Manage google alerts from a google account
    /// </summary>
    /// <param name="user">google account user name (email)</param>
    /// <param name="pass">google account password</param>
    /// <param name="timeout">max time in seconds for the responses</param>
    public GAlerts(string user, string pass, int timeout)
    {
        this.user = user;
        this.pass = pass;
        this.cookie = new CookieContainer();
        this.timeout = timeout;
        this.logged = false;
    }

    /// <summary>
    /// Creates a new alert in the account
    /// </summary>
    /// <param name="query">term to search for</param>
    /// <param name="lang">language of the searches ('en', 'ca', 'es',...)</param>
    /// <param name="frequency">when the alerts are refreshed. Possible values: 'day', 'week', 'happens'</param>
    /// <param name="type">type of the returned results. Possible values: 'all', 'news', 'blogs', 'videos', 'forums','books'</param>
    /// <param name="quantity">number of results all or just the best. Possible values: 'best', 'all'</param>
    /// <param name="dest">destination of the alert. Possible values: 'feed', 'email'</param>
    /// <returns>AlertItem created</returns>
    public AlertItem create(string query, string lang = "en", string frequency = "happens", string type = "all", string quantity = "best", string dest = "feed")
    {
        string e = "feed";
        string t = "7"; //all
        string f = "0"; //when happens
        string l = "0"; //only best

        if (quantity != "best") l = "1";
        if (dest == "email") e = this.user;

        switch (type)
        {
            case "news":
                t = "1";
                break;
            case "blogs":
                t = "4";
                break;
            case "videos":
                t = "9";
                break;
            case "forums":
                t = "8";
                break;
            case "books":
                t = "22";
                break;
        }

        switch (frequency)
        {
            case "day":
                f = "1";
                break;
            case "week":
                f = "6";
                break;
        }

        this.authentication();

        string data = this.sResponse(this.sRequest(this.urlAlerts + "?hl=" + lang));
        int posi = data.IndexOf("name=\"x\" value=\"") + "name=\"x\" value=\"".Length;
        int posf = data.IndexOf("\"", posi + 1);
        string tokenX = data.Substring(posi, posf - posi);

        var post = new Dictionary<string, string>();
        post.Add("x", tokenX);
        post.Add("q", query);
        post.Add("t", t);
        post.Add("f", f);
        post.Add("l", l);
        post.Add("e", e);

        this.sRequest(this.urlCreate + "?hl=" + lang, post);

        string mdata = this.sResponse(this.sRequest(this.urlManage + "?hl=" + lang));
        List<AlertItem> ares = this.parseAlerts(mdata);

        if (ares.Count <= 0)
        {
            throw new GAlertException("Cannot create the alert, alert not found after creation.");
        }

        foreach(AlertItem a in ares) 
        {
            if (a.term == query && a.type == dest)
            {
                return a;
            }
        }

        throw new GAlertException("Cannot create the alert, alert not found after creation.");
    }


    /// <summary>
    /// Deletes an alert from the account
    /// </summary>
    /// <param name="idAlert">Can be obtained with create() or getList() methods</param>
    /// <returns>true when success</returns>
    public bool delete(string idAlert)
    {
        this.authentication();
        string res = this.sResponse(this.sRequest(this.urlManage + "?hl=en"));

        int posi = res.IndexOf("name=\"x\" value=\"") + "name=\"x\" value=\"".Length;
        int posf = res.IndexOf("\"", posi + 1);
        string tokenX = res.Substring(posi, posf - posi);

        var post = new Dictionary<string, string>();
        post.Add("x", tokenX);
        post.Add("s", idAlert);
        post.Add("da", "Delete");

        HttpWebRequest req = this.sRequest(this.urlDelete + "?hl=en", post);

        if (this.sResponse(req, true).StatusCode != HttpStatusCode.OK)
        {
            throw new GAlertException("Cannot delete the alert, bad response from server");
        }

        return true;
    }

    /// <summary>
    /// List with all the current alerts in the account
    /// </summary>
    /// <returns>list of alerts currently in the account</returns>
    public List<AlertItem> getList()
    {
        this.authentication();
        string res = this.sResponse(this.sRequest(this.urlManage));
        return this.parseAlerts(res);
    }   


    protected void authentication()
    {
        if (!this.logged)
        {
            string data = this.sResponse(this.sRequest(this.urlLogin));
            Dictionary<string, string> formFields = this.getFormFields(data);
            formFields["Email"] = this.user;
            formFields["Passwd"] = this.pass;
            formFields.Remove("PersistentCookie");

            HttpWebRequest req = this.sRequest(this.urlAuth, formFields);
            string res = this.sResponse(req);
            this.logged = true;
        }
    }

    protected Dictionary<string, string> getFormFields(string data)
    {
        Match match=Regex.Match(data, "(<form.*?id=.?gaia_loginform.*?<\\/form>)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Captures.Count>0)
        {
            Dictionary<string, string>  inputs = this.getInputs(match.Captures[0].Value);
            return inputs;
        }

        return null;
    }

    protected Dictionary<string, string> getInputs(string form)
    {
        Dictionary<string, string> res = new Dictionary<string, string>();

        MatchCollection matches = Regex.Matches(form, "(<input[^>]+>)", RegexOptions.IgnoreCase);
        if (matches.Count > 0)
        {
            for (int i = 0; i < matches.Count; i++)
            {
                string el = Regex.Replace(((Match)matches[i]).Captures[0].Value, "\\s{2,}", " ");
                Match m=Regex.Match(el, "name=(?:[\"'])?([^\"'\\s]*)", RegexOptions.IgnoreCase);
                if (m.Groups.Count > 0)
                {
                    string name = m.Groups[1].Value;
                    string value = "";

                    Match n = Regex.Match(el, "value=(?:[\"'])?([^\"'\\s]*)", RegexOptions.IgnoreCase);
                    if (n.Groups.Count > 0)
                    {
                        value = n.Groups[1].Value;
                    }
                    try
                    {
                        res.Add(name, value);
                    }
                    catch(Exception e)
                    { }
                }
            }
        }

        return res;
    }

    protected List<AlertItem> parseAlerts(string data)
    {
        List<AlertItem> res=new List<AlertItem>();
        string regexp = "(?:<tr id=\"(.*?)\" class=\"ACTIVE\">)(.*?)(?:<\\/tr>)";
        string regexp2= "(?:alert-type\" colspan=\"2\"><a href=\"[^\"]*\">)(.*?)(?:<\\/a>)";
        string regexp3="Feed<\\/a> <a href=\"([^\"]*)\">(.*?)<\\/a>";

        MatchCollection mc=Regex.Matches(data, regexp, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        int cnt=0;
        foreach(Match m in mc)
        {
            AlertItem ai=new AlertItem();
            ai.id = m.Groups[1].Value;
            if (m.Groups[2].Value.IndexOf("Feed")>0) {
                ai.type="feed";
            } else {
                ai.type="email";
            }

            MatchCollection rrow=Regex.Matches(m.Groups[2].Value, regexp2);
            if (rrow.Count>0)
            {
                ai.term=rrow[0].Groups[1].Value;
            }

            rrow=Regex.Matches(m.Groups[2].Value, regexp3);
            if (rrow.Count>0)
            {
                ai.url=rrow[0].Groups[1].Value;
            }

            cnt++;
            res.Add(ai);
        }

        return res;
    }

    private HttpWebResponse sResponse(HttpWebRequest req, bool reqRef)
    {
        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        if (reqRef)
        {
            Stream stmResp = resp.GetResponseStream();
            StreamReader strRead = new StreamReader(stmResp, Encoding.UTF8);

            string strResp = strRead.ReadToEnd();

            resp.Close();
            stmResp.Close();
            strRead.Close();
            return resp;
        }
        return resp;
    }

    private string sResponse(HttpWebRequest req)
    {
        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        Stream stmResp = resp.GetResponseStream();
        StreamReader strRead = new StreamReader(stmResp, Encoding.UTF8);

        string strResp = strRead.ReadToEnd();

        resp.Close();
        stmResp.Close();
        strRead.Close();

        return strResp;
    }

    private HttpWebRequest sRequest(string url)
    {
        return this.sRequest(url, null);
    }

    private HttpWebRequest sRequest(string url, Dictionary<string, string> post)
    {

        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.MaximumAutomaticRedirections = this.maxRedirs;
        req.Referer = this.urlAlerts;
        req.UserAgent = this.userAgent;
        req.CookieContainer = this.cookie;
        req.Timeout = this.timeout * 1000;

        if (post != null)
        {
            string data = "";
            foreach (var kv in post)
            {
                if (data != "") data += "&";
                data += kv.Key + "=" + Uri.EscapeUriString(kv.Value);

            }

            byte[] dataStream = Encoding.UTF8.GetBytes(data);

            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = dataStream.Length;

            Stream sstrm = req.GetRequestStream();
            sstrm.Write(dataStream, 0, dataStream.Length);
            sstrm.Close();
        }

        return req;
    }

}

