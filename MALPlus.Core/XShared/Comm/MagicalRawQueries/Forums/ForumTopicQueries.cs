using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MALClient.Models.Enums;
using MALClient.Models.Models;
using MALClient.Models.Models.Forums;
using MALClient.XShared.Utils;
using MALClient.XShared.ViewModels;
using MALClient.XShared.ViewModels.Forums.Items;
using Newtonsoft.Json;

namespace MALClient.XShared.Comm.MagicalRawQueries.Forums
{
    public static class ForumTopicQueries
    {
        #region Create
        /// <summary>
        /// Creates forum or club topic.
        /// </summary>
        /// <param name="title">Topic title</param>
        /// <param name="message">OP message content</param>
        /// <param name="type">Whether standard forum or clubs</param>
        /// <param name="id">Id of board or club</param>
        /// <param name="question"></param>
        /// <param name="answers"></param>
        /// <returns></returns>
        public static Task<Tuple<bool, string>> CreateNewTopic(string title, string message, TopicType type, int id,
            string question = null, List<string> answers = null)
        {
            return CreateNewTopic(title, message, type == TopicType.Anime
                ? $"https://myanimelist.net/forum/?action=post&anime_id={id}"
                : $"https://myanimelist.net/forum/?action=post&manga_id={id}", question, answers);
        }

        public static Task<Tuple<bool, string>> CreateNewTopic(string title, string message, ForumType type, int id,
            string question = null, List<string> answers = null)
        {
            return CreateNewTopic(title, message, type == ForumType.Normal
                ? $"https://myanimelist.net/forum/?action=post&boardid={id}"
                : $"https://myanimelist.net/forum/?action=post&club_id={id}", question, answers);
        }
        private static async Task<Tuple<bool,string>> CreateNewTopic(string title, string message, string endpoint, string question = null, List<string> answers = null)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("topic_title", title),
                    new KeyValuePair<string, string>("msg_text", message),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                    new KeyValuePair<string, string>("submit", "Submit")
                };

                if (!string.IsNullOrEmpty(question) && answers != null)
                {
                    if (answers.Count > 0)
                    {
                        data.Add(new KeyValuePair<string, string>("pollQuestion", question));
                        data.AddRange(answers.Select(answer => new KeyValuePair<string, string>("pollOption[]", answer)));
                    }
                }

                var requestContent = new FormUrlEncodedContent(data);

                var response = await client.PostAsync(endpoint, requestContent);
                //var response =
                //    await client.PostAsync(
                //        "/forum/?action=post&club_id=73089", requestContent);

                if (!response.IsSuccessStatusCode)
                    return new Tuple<bool, string>(false,null);

                try
                {
                    var resp = await response.Content.ReadAsStringAsync();
                    if (resp.Contains("badresult"))
                    {
                        if(resp.Contains("The given value for $val"))
                            return new Tuple<bool, string>(true, null);
                        return new Tuple<bool, string>(false,null);
                    }
                    var doc = new HtmlDocument();
                    doc.LoadHtml(resp);
                    var wrapper = doc.FirstOfDescendantsWithId("div", "contentWrapper");
                    var matches = Regex.Match(wrapper.InnerHtml, @"topicid=(\d+)");
                    return new Tuple<bool, string>(true,matches.Groups[1].Value);
                }
                catch (Exception)
                {
                   return new Tuple<bool, string>(true,null);
                }



            }
            catch (Exception)
            {
                return new Tuple<bool, string>(false, null);
            }
        }


        /// <summary>
        /// Creates message in a topic
        /// </summary>
        /// <param name="id">Id of the topic</param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task<bool> CreateMessage(string id, string message)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("msg_text", message),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                    new KeyValuePair<string, string>("action_type", "submit")
                };

                var requestContent = new FormUrlEncodedContent(data);

                var response =
                    await client.PostAsync(
                        $"https://myanimelist.net/forum/?action=message&topic_id={id}", requestContent);

                if (!response.IsSuccessStatusCode)
                    return false;

                var body = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(body))
                    return false;

                // A successful post re-renders the topic page; captcha/login/error pages mean failure
                if (body.Contains("g-recaptcha") || body.Contains("action=login") || body.Contains("badresult"))
                    return false;

                return true;
            }
            catch (Exception e)
            {
                return false;
            }

        }
        #endregion

        #region Edit

        class MessageHtmlResponse
        {
            public string message_html { get; set; }
        }

        /// <summary>
        /// Takes bbcode and updates message.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="message"></param>
        /// <param name="topicId"></param>
        /// <returns>Returns html content of the message</returns>
        public static async Task<string> EditMessage(string id, string message, string topicId)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("msg_id", id),
                    new KeyValuePair<string, string>("msg", message),
                    new KeyValuePair<string, string>("topic_id", topicId),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                };

                var requestContent = new FormUrlEncodedContent(data);

                var response =
                    await client.PostAsync(
                        $"https://myanimelist.net/includes/ajax.inc.php?t=86", requestContent);

                if (!response.IsSuccessStatusCode)
                    return null;


                var responseData = JsonConvert.DeserializeObject<MessageHtmlResponse>(await response.Content.ReadAsStringAsync());
                if (string.IsNullOrWhiteSpace(responseData?.message_html))
                    return null;
                var presentationHtml = SanitizePresentationHtml(responseData.message_html);
                var normalizedId = NormalizeMessageId(id);
                if (!string.IsNullOrWhiteSpace(topicId) && normalizedId != null &&
                    CachedMessagesDictionary.TryGetValue(topicId, out var topicCache))
                {
                    foreach (var cachedPage in topicCache.Values)
                    {
                        var cachedMessage = cachedPage?.Messages?.FirstOrDefault(entry => entry.Id == normalizedId);
                        if (cachedMessage != null)
                            cachedMessage.HtmlContent = presentationHtml;
                    }
                }
                return presentationHtml;
            }
            catch (Exception)
            {
                return null;
            }

        }


        class MessageBbcodeResponse
        {
            public string message { get; set; }
        }

        /// <summary>
        /// Turns message into editable bbcode
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static async Task<string> GetMessageBbcode(string id)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("msg_id", id),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                };

                var requestContent = new FormUrlEncodedContent(data);

                var response =
                    await client.PostAsync(
                        $"https://myanimelist.net/includes/ajax.inc.php?t=85", requestContent);

                if (!response.IsSuccessStatusCode)
                    return null;

                return
                    JsonConvert.DeserializeObject<MessageBbcodeResponse>(await response.Content.ReadAsStringAsync())
                        .message;
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion

        #region DeleteComment

        public static async Task<bool> DeleteComment(string id)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("msgId", id),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                };

                var requestContent = new FormUrlEncodedContent(data);

                var response =
                    await client.PostAsync(
                        "https://myanimelist.net/includes/ajax.inc.php?t=84", requestContent);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }

        }

        #endregion

        #region GetTopicData

        public static string BuildTopicPresentationHtml(ForumTopicData data)
        {
            var builder = new StringBuilder();
            builder.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><base href=\"https://myanimelist.net/\"><style>");
            builder.Append("html,body{margin:0;padding:0;background:#051522;color:#d4e4f7;}body{font-family:Inter,Arial,sans-serif;font-size:15px;line-height:1.5;word-wrap:break-word;}.message{background:#0d1d2c;border:1px solid #1e3a52;border-radius:10px;margin:0 0 10px;padding:12px;}.message-header{color:#fff;font-weight:600;margin-bottom:4px;}.message-meta{color:#a0b5c8;font-size:12px;font-weight:400;margin-top:2px;}.message-content{overflow-wrap:anywhere;}.message-content p{margin:0 0 8px;}.message-content p:last-child{margin-bottom:0;}.message-content blockquote{border-left:3px solid #0066ff;background:#10283d;color:#b9cde0;margin:10px 0;padding:8px 12px;}.message-content a{color:#4da3ff;}.message-content img{max-width:100%;height:auto;}.message-content pre,.message-content code{font-family:monospace;white-space:pre-wrap;}");
            // Post bodies arrive from MAL without a stylesheet, so headings, lists
            // and tables fell back to browser defaults and the opening post rendered
            // in giant type. Pin them to the body size.
            builder.Append(".message-content h1,.message-content h2,.message-content h3,.message-content h4,.message-content h5,.message-content h6{font-size:15px;font-weight:600;color:#fff;margin:0 0 8px;}.message-content ul,.message-content ol{margin:0 0 8px;padding-left:20px;}.message-content li{margin:0 0 4px;}.message-content table{border-collapse:collapse;width:100%;margin:0 0 8px;}.message-content td,.message-content th{border:1px solid #1e3a52;padding:6px 8px;text-align:left;vertical-align:top;}.message-content hr{border:0;border-top:1px solid #1e3a52;margin:10px 0;}.message-content small{font-size:12px;}");
            // Post bodies keep MAL's own inline colours (black on a dark sheet is
            // invisible), so the palette is forced back on every descendant.
            builder.Append(".message-content *{color:#d4e4f7 !important;}.message-content a{color:#4da3ff !important;}.message-content h1,.message-content h2,.message-content h3,.message-content h4,.message-content h5,.message-content h6{color:#fff !important;}.message-content blockquote,.message-content blockquote *{color:#b9cde0 !important;}");
            builder.Append("#op-toggle{display:block;width:100%;margin:4px 0 0;padding:10px 12px;border:1px solid #1e3a52;border-radius:10px;background:#0d1d2c;color:#4da3ff;font-family:Inter,Arial,sans-serif;font-size:13px;font-weight:600;}.op-collapsed{display:none;}");
            builder.Append("</style></head><body>");
            if (data?.Messages != null)
            {
                var firstMessage = true;
                foreach (var message in data.Messages)
                {
                    if (message == null)
                        continue;
                    // The opening post is the long one, so it starts collapsed and the
                    // button at the bottom reveals it.
                    builder.Append(firstMessage
                        ? "<article id=\"op\" class=\"message op-collapsed\" data-id=\""
                        : "<article class=\"message\" data-id=\"");
                    firstMessage = false;
                    builder.Append(WebUtility.HtmlEncode(NormalizeMessageId(message.Id) ?? string.Empty));
                    builder.Append("\"><header class=\"message-header\">");
                    builder.Append(WebUtility.HtmlEncode(message.Poster?.MalUser?.Name ?? "Unknown"));
                    builder.Append("</header>");
                    var messageNumber = NormalizeMessageNumber(message.MessageNumber);
                    if (!string.IsNullOrWhiteSpace(messageNumber))
                    {
                        builder.Append("<p class=\"message-meta\">#");
                        builder.Append(WebUtility.HtmlEncode(messageNumber));
                    builder.Append(" &middot; ");
                        builder.Append(WebUtility.HtmlEncode(message.CreateDate ?? string.Empty));
                        builder.Append("</p>");
                    }
                    if (!string.IsNullOrWhiteSpace(message.EditDate))
                    {
                        builder.Append("<p class=\"message-meta\">");
                        builder.Append(WebUtility.HtmlEncode(message.EditDate));
                        builder.Append("</p>");
                    }
                    builder.Append("<section class=\"message-content\">");
                    builder.Append(SanitizePresentationHtml(message.HtmlContent));
                    builder.Append("</section></article>");
                }

                if (!firstMessage)
                {
                    builder.Append("<button id=\"op-toggle\" type=\"button\">Show first post</button>");
                    builder.Append("<script>var b=document.getElementById('op-toggle'),op=document.getElementById('op');" +
                                   "b.onclick=function(){var c=op.classList.toggle('op-collapsed');" +
                                   "b.textContent=c?'Show first post':'Hide first post';};</script>");
                }
            }
            builder.Append("</body></html>");
            return builder.ToString();
        }

        public static string SanitizePresentationHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            try
            {
                var document = new HtmlDocument();
                document.LoadHtml(html);
                var root = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;
                var builder = new StringBuilder();
                foreach (var child in root.ChildNodes)
                    AppendPresentationNode(child, builder);
                return builder.ToString().Trim();
            }
            catch (Exception)
            {
                return WebUtility.HtmlEncode(html);
            }
        }

        private static void AppendPresentationNode(HtmlNode node, StringBuilder builder)
        {
            if (node == null)
                return;
            if (node.NodeType == HtmlNodeType.Text)
            {
                AppendPresentationText(WebUtility.HtmlDecode(node.InnerText), builder);
                return;
            }
            if (node.NodeType != HtmlNodeType.Element)
                return;

            var name = node.Name.ToLowerInvariant();
            if (IsIgnoredPresentationNode(node, name))
                return;
            if (name == "br")
            {
                builder.Append("<br />");
                return;
            }
            if (name == "img")
            {
                AppendPresentationImage(node, builder);
                return;
            }
            if (name == "hr")
            {
                builder.Append("<hr />");
                return;
            }
            if (name == "a")
            {
                var href = GetSafePresentationUrl(GetAttributeValue(node, "href"));
                if (href == null)
                {
                    foreach (var child in node.ChildNodes)
                        AppendPresentationNode(child, builder);
                    return;
                }
                builder.Append("<a href=\"");
                builder.Append(WebUtility.HtmlEncode(href));
                builder.Append("\" target=\"_blank\" rel=\"nofollow noopener noreferrer\">");
                foreach (var child in node.ChildNodes)
                    AppendPresentationNode(child, builder);
                builder.Append("</a>");
                return;
            }

            var outputTag = name switch
            {
                "b" or "strong" or "i" or "em" or "u" or "s" or "strike" or "p" or "blockquote" or
                "code" or "pre" or "ul" or "ol" or "li" or "dl" or "dt" or "dd" or "q" or "cite" or
                "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "small" or "mark" or "span" => name,
                "font" => "span",
                _ => null
            };
            if (name == "div" && HasClass(node, "quotetext"))
                outputTag = "blockquote";

            if (outputTag != null)
                builder.Append('<').Append(outputTag).Append('>');
            foreach (var child in node.ChildNodes)
                AppendPresentationNode(child, builder);
            if (outputTag != null)
                builder.Append("</").Append(outputTag).Append('>');
        }

        private static void AppendPresentationText(string text, StringBuilder builder)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                if (!string.IsNullOrEmpty(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0)
                    builder.Append(' ');
                return;
            }
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            builder.Append(WebUtility.HtmlEncode(normalized).Replace("\n", "<br />"));
        }

        private static void AppendPresentationImage(HtmlNode node, StringBuilder builder)
        {
            var source = GetSafePresentationUrl(GetAttributeValue(node, "data-src") ?? GetAttributeValue(node, "src"));
            if (source == null)
                return;
            builder.Append("<img src=\"");
            builder.Append(WebUtility.HtmlEncode(source));
            builder.Append("\" loading=\"eager\" alt=\"");
            builder.Append(WebUtility.HtmlEncode(GetAttributeValue(node, "alt") ?? string.Empty));
            builder.Append("\" />");
        }

        private static bool IsIgnoredPresentationNode(HtmlNode node, string name)
        {
            if (name == "script" || name == "style" || name == "noscript" || name == "iframe" ||
                name == "object" || name == "embed" || name == "form" || name == "input" ||
                name == "button" || name == "select" || name == "textarea")
                return true;
            var classes = (GetAttributeValue(node, "class") ?? string.Empty)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return classes.Any(value => value.Equals("postActions", StringComparison.OrdinalIgnoreCase) ||
                                        value.Equals("sig", StringComparison.OrdinalIgnoreCase) ||
                                        value.Equals("sig-container", StringComparison.OrdinalIgnoreCase) ||
                                        value.Equals("signature", StringComparison.OrdinalIgnoreCase) ||
                                        value.StartsWith("sig-", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetSafePresentationUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var url = WebUtility.HtmlDecode(value).Trim();
            if (url.StartsWith("//", StringComparison.Ordinal))
                return "https:" + url;
            if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            {
                if (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                    absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                    absolute.Scheme.Equals(Uri.UriSchemeMailto, StringComparison.OrdinalIgnoreCase))
                    return absolute.AbsoluteUri;
                return null;
            }
            if (url.StartsWith("/", StringComparison.Ordinal) || url.StartsWith("#", StringComparison.Ordinal) ||
                url.StartsWith("?", StringComparison.Ordinal))
                return url;
            if (url.IndexOf(':') < 0)
                return url;
            return null;
        }

        private static string GetAttributeValue(HtmlNode node, string name)
        {
            return node?.Attributes != null && node.Attributes.Contains(name) ? node.Attributes[name].Value : null;
        }

        private static bool HasClass(HtmlNode node, string className)
        {
            var classes = (GetAttributeValue(node, "class") ?? string.Empty)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (className.IndexOf(' ') >= 0)
                return string.Join(" ", classes).IndexOf(className, StringComparison.OrdinalIgnoreCase) >= 0;
            return classes.Any(value => value.Equals(className, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeMessageId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var match = Regex.Match(WebUtility.HtmlDecode(value), @"(?:forumMsg|msg)?([0-9]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string NormalizeMessageNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var match = Regex.Match(WebUtility.HtmlDecode(value), @"\d+");
            return match.Success ? match.Value : null;
        }

        private static readonly Dictionary<string, Dictionary<int, ForumTopicData>> CachedMessagesDictionary =
            new Dictionary<string, Dictionary<int, ForumTopicData >>();

        public static async Task<ForumTopicData> GetTopicData(string topicId,int page,bool lastpage = false,long? messageId = null,bool force = false)
        {
            page = Math.Max(1, page);
            if (string.IsNullOrWhiteSpace(topicId))
                topicId = null;
            if (topicId == null && messageId == null)
                return null;

            if (!force && !lastpage && messageId == null && topicId != null &&
                CachedMessagesDictionary.TryGetValue(topicId, out var cachedTopic) &&
                cachedTopic.TryGetValue(page, out var cachedData))
                return cachedData;

            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();
                if (client == null)
                    return null;

                var requestUri = new Uri(lastpage
                    ? $"https://myanimelist.net/forum/?topicid={topicId}&goto=lastpost"
                    : messageId != null
                        ? $"https://myanimelist.net/forum/message/{messageId}?goto=topic"
                        : $"https://myanimelist.net/forum/?topicid={topicId}&show={(page - 1) * 50}");
                var response = await client.GetAsync(requestUri.AbsoluteUri);
                var currentUri = requestUri;
                var redirectCount = 0;
                while (response != null && IsRedirect(response.StatusCode) && redirectCount++ < 3)
                {
                    var location = response.Headers.Location;
                    if (location == null)
                    {
                        response.Dispose();
                        return null;
                    }
                    currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    response.Dispose();
                    response = await client.GetAsync(currentUri.AbsoluteUri);
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    response?.Dispose();
                    return null;
                }

                var finalUri = response.RequestMessage?.RequestUri ?? currentUri;
                var html = await response.Content.ReadAsStringAsync();
                response.Dispose();
                if (string.IsNullOrWhiteSpace(html))
                    return null;

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                topicId = topicId ?? GetQueryValue(finalUri, "topicid");
                if (string.IsNullOrWhiteSpace(topicId))
                {
                    var canonical = doc.DocumentNode.Descendants("link")
                        .FirstOrDefault(node => string.Equals(GetAttributeValue(node, "rel"), "canonical", StringComparison.OrdinalIgnoreCase));
                    if (Uri.TryCreate(new Uri("https://myanimelist.net/"), GetAttributeValue(canonical, "href"), out var canonicalUri))
                        topicId = GetQueryValue(canonicalUri, "topicid");
                }
                if (string.IsNullOrWhiteSpace(topicId))
                {
                    var topicMatch = Regex.Match(html, @"topicid=(\d+)", RegexOptions.IgnoreCase);
                    if (topicMatch.Success)
                        topicId = topicMatch.Groups[1].Value;
                }
                if (string.IsNullOrWhiteSpace(topicId))
                    return null;

                var output = new ForumTopicData { Id = topicId };
                if (messageId != null && messageId > 0)
                    output.TargetMessageId = NormalizeMessageId(messageId.ToString());

                var pagination = ReadPagination(doc, finalUri, page, lastpage);
                output.AllPages = pagination.AllPages;
                output.CurrentPage = pagination.CurrentPage;

                var titleNode = doc.DocumentNode.Descendants("h1")
                    .FirstOrDefault(node => HasClass(node, "forum_locheader"));
                output.Title = WebUtility.HtmlDecode(titleNode?.InnerText?.Trim());
                output.IsLocked = titleNode != null && HasClass(titleNode, "icon-forum-locked");
                if (string.IsNullOrWhiteSpace(output.Title))
                {
                    titleNode = doc.DocumentNode.Descendants("h1")
                        .FirstOrDefault(node => HasClass(node, "forum_locheader") && HasClass(node, "icon-forum-locked"));
                    output.Title = WebUtility.HtmlDecode(titleNode?.InnerText?.Trim());
                    output.IsLocked = titleNode != null;
                }

                var breadcrumb = FindFirstByClass(doc.DocumentNode, "breadcrumb");
                if (breadcrumb != null)
                {
                    foreach (var breadcrumbNode in breadcrumb.ChildNodes.Where(node => node.Name.Equals("div", StringComparison.OrdinalIgnoreCase)))
                    {
                        var link = breadcrumbNode.Descendants("a").FirstOrDefault();
                        if (link == null)
                            continue;
                        output.Breadcrumbs.Add(new ForumBreadcrumb
                        {
                            Name = WebUtility.HtmlDecode(breadcrumbNode.InnerText?.Trim() ?? string.Empty),
                            Link = GetAttributeValue(link, "href") ?? string.Empty
                        });
                    }
                }

                var foundMembers = new Dictionary<string, MalForumUser>();
                foreach (var row in doc.DocumentNode.Descendants("div")
                    .Where(node => HasClass(node, "forum-topic-message")))
                {
                    var messageIdValue = NormalizeMessageId(GetAttributeValue(row, "data-id")) ??
                                        NormalizeMessageId(GetAttributeValue(row, "id"));
                    if (string.IsNullOrWhiteSpace(messageIdValue))
                        continue;

                    var current = new ForumMessageEntry { TopicId = topicId, Id = messageIdValue };
                    var dateNode = FindFirstByClass(row, "date");
                    current.CreateDate = WebUtility.HtmlDecode(dateNode?.InnerText?.Trim() ?? string.Empty);
                    var postNode = FindFirstByClass(row, "postnum");
                    current.MessageNumber = NormalizeMessageNumber(postNode?.InnerText) ??
                                            NormalizeMessageNumber(GetAttributeValue(postNode, "data-postnum"));
                    if (current.MessageNumber == null)
                    {
                        var postLink = postNode?.Descendants("a").FirstOrDefault();
                        current.MessageNumber = NormalizeMessageNumber(postLink?.InnerText);
                    }

                    var posterName = WebUtility.HtmlDecode(FindFirstByClass(row, "username")?.InnerText?.Trim());
                    if (string.IsNullOrWhiteSpace(posterName))
                        posterName = "Unknown";
                    if (foundMembers.TryGetValue(posterName, out var foundPoster))
                    {
                        current.Poster = foundPoster;
                    }
                    else
                    {
                        var poster = new MalForumUser();
                        poster.MalUser.Name = posterName;
                        var titlePosterNode = FindFirstByClass(row, "custom-forum-title");
                        poster.Title = WebUtility.HtmlDecode(titlePosterNode?.InnerText?.Trim());
                        var forumIcon = row.Descendants("a").FirstOrDefault(node => HasClass(node, "forum-icon"));
                        var posterImage = forumIcon?.Descendants("img")
                            .FirstOrDefault(node => !string.IsNullOrWhiteSpace(GetAttributeValue(node, "data-src")) &&
                                                    GetAttributeValue(node, "data-src").Contains("useravatars", StringComparison.OrdinalIgnoreCase))
                            ?? forumIcon?.Descendants("img").FirstOrDefault();
                        poster.MalUser.ImgUrl = GetAttributeValue(posterImage, "data-src") ??
                                                 GetAttributeValue(posterImage, "src");
                        poster.Status = WebUtility.HtmlDecode(FindFirstByClass(row, "userstatus")?.InnerText?.Trim());
                        poster.Joined = WebUtility.HtmlDecode(FindFirstByClass(row, "userinfo joined")?.InnerText?.Trim());
                        poster.Posts = WebUtility.HtmlDecode(FindFirstByClass(row, "userinfo posts")?.InnerText?.Trim());
                        poster.SignatureHtml = null;
                        foundMembers[posterName] = poster;
                        current.Poster = poster;
                    }

                    var editNode = FindFirstByClass(row, "modified");
                    if (editNode != null)
                        current.EditDate = "Modified by " + string.Join(" ", editNode.ChildNodes.Select(node => WebUtility.HtmlDecode(node.InnerText)?.Trim() ?? string.Empty));

                    var contentNode = FindFirstByClass(row, "content") ?? FindFirstByClassContaining(row, "message-text");
                    current.HtmlContent = SanitizePresentationHtml(contentNode?.OuterHtml);
                    var actions = FindFirstByClass(row, "postActions");
                    if (actions != null)
                    {
                        current.CanEdit = actions.Descendants().Any(node => node.InnerText?.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0);
                        current.CanDelete = actions.Descendants().Any(node => node.InnerText?.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0);
                    }
                    output.Messages.Add(current);
                }
                
                if (output.Messages.Count == 0)
                    return null;

                var responsePage = GetPageFromUri(finalUri, 0);
                var canCache = (!lastpage && messageId == null) ||
                               (lastpage && messageId == null && responsePage > 1 && output.CurrentPage == responsePage);
                if (canCache && topicId != null)
                {
                    if (!CachedMessagesDictionary.TryGetValue(topicId, out var topicCache))
                    {
                        topicCache = new Dictionary<int, ForumTopicData>();
                        CachedMessagesDictionary[topicId] = topicCache;
                    }
                    topicCache[output.CurrentPage] = output;
                }

                return output;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsRedirect(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.MovedPermanently ||
                   statusCode == HttpStatusCode.Found ||
                   statusCode == HttpStatusCode.SeeOther ||
                   statusCode == HttpStatusCode.TemporaryRedirect ||
                   (int)statusCode == 308;
        }

        private static string GetQueryValue(Uri uri, string name)
        {
            if (uri == null || string.IsNullOrEmpty(uri.Query))
                return null;
            foreach (var part in uri.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (!string.Equals(WebUtility.UrlDecode(pair[0]), name, StringComparison.OrdinalIgnoreCase))
                    continue;
                return pair.Length > 1 ? WebUtility.UrlDecode(pair[1]) : string.Empty;
            }
            return null;
        }

        private static int GetPageFromUri(Uri uri, int fallback)
        {
            var show = GetQueryValue(uri, "show");
            if (int.TryParse(show, out var offset) && offset >= 0)
                return offset / 50 + 1;
            var page = GetQueryValue(uri, "page");
            if (int.TryParse(page, out var pageNumber) && pageNumber > 0)
                return pageNumber;
            return fallback;
        }

        private static (int AllPages, int CurrentPage) ReadPagination(HtmlDocument document, Uri responseUri, int requestedPage, bool lastpage)
        {
            var allPages = 0;
            var currentPage = 0;
            var pager = document.DocumentNode.Descendants("div")
                .FirstOrDefault(node => HasClass(node, "pages") || HasClass(node, "fl-r pb4"));
            if (pager != null)
            {
                var pagerText = WebUtility.HtmlDecode(pager.InnerText) ?? string.Empty;
                var totalMatch = Regex.Match(pagerText, @"Pages\s*\(\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
                if (totalMatch.Success && int.TryParse(totalMatch.Groups[1].Value, out var parsedTotal))
                    allPages = parsedTotal;

                foreach (var anchor in pager.Descendants("a"))
                {
                    var text = WebUtility.HtmlDecode(anchor.InnerText)?.Trim() ?? string.Empty;
                    var hrefPage = GetPageFromHref(GetAttributeValue(anchor, "href"));
                    var textPage = NormalizeMessageNumber(text);
                    var dataPage = NormalizeMessageNumber(GetAttributeValue(anchor, "data-page") ??
                                                         GetAttributeValue(anchor, "data-page-number"));
                    if (int.TryParse(textPage, out var parsedTextPage))
                        allPages = Math.Max(allPages, parsedTextPage);
                    if (int.TryParse(dataPage, out var parsedDataPage))
                        allPages = Math.Max(allPages, parsedDataPage);
                    if (text.IndexOf("last", StringComparison.OrdinalIgnoreCase) >= 0 && hrefPage > 0)
                        allPages = Math.Max(allPages, hrefPage);
                    if (HasClass(anchor, "current") || HasClass(anchor, "selected") || HasClass(anchor, "active"))
                    {
                        if (int.TryParse(textPage, out var parsedCurrentPage))
                            currentPage = parsedCurrentPage;
                        else if (int.TryParse(dataPage, out var parsedCurrentDataPage))
                            currentPage = parsedCurrentDataPage;
                        else if (hrefPage > 0)
                            currentPage = hrefPage;
                    }
                }

                foreach (var currentNode in pager.Descendants()
                    .Where(node => HasClass(node, "current") || HasClass(node, "selected") || HasClass(node, "active")))
                {
                    var currentText = NormalizeMessageNumber(WebUtility.HtmlDecode(currentNode.InnerText));
                    if (int.TryParse(currentText, out var parsedCurrentNode))
                        currentPage = parsedCurrentNode;
                }

                foreach (Match match in Regex.Matches(pagerText, @"\[\s*(\d+)\s*\]", RegexOptions.IgnoreCase))
                {
                    if (int.TryParse(match.Groups[1].Value, out var markerPage))
                        currentPage = markerPage;
                }
            }

            var uriPage = GetPageFromUri(responseUri, 0);
            if (lastpage && uriPage > 0)
                currentPage = uriPage;
            else if (currentPage <= 0)
                currentPage = uriPage;
            if (currentPage <= 0)
                currentPage = Math.Max(1, requestedPage);
            if (allPages <= 0)
                allPages = currentPage;
            if (lastpage && uriPage <= 0)
                currentPage = allPages;
            if (allPages > 0 && currentPage > allPages)
                currentPage = allPages;
            allPages = Math.Max(allPages, currentPage);
            return (allPages, currentPage);
        }

        private static int GetPageFromHref(string href)
        {
            if (string.IsNullOrWhiteSpace(href) ||
                !Uri.TryCreate(new Uri("https://myanimelist.net/"), WebUtility.HtmlDecode(href), out var uri))
                return 0;
            return GetPageFromUri(uri, 0);
        }

        private static HtmlNode FindFirstByClass(HtmlNode root, string className)
        {
            return root?.Descendants().FirstOrDefault(node => HasClass(node, className));
        }

        private static HtmlNode FindFirstByClassContaining(HtmlNode root, string className)
        {
            return root?.Descendants().FirstOrDefault(node =>
                (GetAttributeValue(node, "class") ?? string.Empty)
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(value => value.IndexOf(className, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        public static void NotifyMessageRemoved(ForumMessageEntry forumMessage)
        {
            if (forumMessage == null || string.IsNullOrWhiteSpace(forumMessage.TopicId) ||
                !CachedMessagesDictionary.TryGetValue(forumMessage.TopicId, out var topicCache))
                return;
            foreach (var page in topicCache)
            {
                if (page.Value?.Messages == null)
                    continue;
                var index = page.Value.Messages.FindIndex(entry => entry.Id == forumMessage.Id);
                if (index != -1)
                {
                    page.Value.Messages.RemoveAt(index);
                    break;
                }
            }
        }

        #endregion

        #region Watch/UnWatch

        /// <summary>
        /// Change topic watching status.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Returns null when failed, true when topic is being watched and false when it's not.</returns>
        public static async Task<bool?> ToggleTopicWatching(string id)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("topic_id", id),
                    new KeyValuePair<string, string>("timestamp",
                        $"{DateTime.Now.ToLocalTime():ddd MMM dd yyyy hh:mm:ss \"GMT\"K} ({TimeZoneInfo.Local.StandardName})"),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                };

                var requestContent = new FormUrlEncodedContent(data);

                var response =
                    await client.PostAsync(
                        "https://myanimelist.net/includes/ajax.inc.php?t=69", requestContent);

                if (!response.IsSuccessStatusCode)
                    return null;

                return (await response.Content.ReadAsStringAsync()) == "Watching";
            }
            catch (Exception)
            {
                return null;
            }

        }
        #endregion

        #region Quote

        /// <summary>
        /// Gets quote string
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Quutable bbcode</returns>
        public static async Task<string> GetQuote(string id)
        {
            try
            {
                var client = await ResourceLocator.MalHttpContextProvider.GetHttpContextAsync();

                var data = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("msgid", id),
                    new KeyValuePair<string, string>("csrf_token", client.Token),
                };

                var requestContent = new FormUrlEncodedContent(data);

                var response =
                    await client.PostAsync(
                        "https://myanimelist.net/includes/quotetext.php", requestContent);

                if (!response.IsSuccessStatusCode)
                    return null;

                return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            }
            catch (Exception)
            {
                return null;
            }

        }

        #endregion
        //

    }
}
