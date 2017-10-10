using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace SimpleHttp
{

    public enum ReferenceHold : byte
    {
        WithScene = 0,
        WaitForComplete = 1
    }

    public enum HttpMethod : byte
    {
        GET = 0,
        POST = 1
    }

    /// <summary>
    /// HttpSender is a dummy class added to run coroutines on temporary GameObjects
    /// </summary>
    public class HttpSender : MonoBehaviour
    {
        public string GUID = "";
        public HttpMethod Method = HttpMethod.GET;
        public string Url = "";
        public string Data = "";
        public int Timeout = 10;
        public System.Action<string, string, HttpSender> Completion;
        public ReferenceHold RefHold = ReferenceHold.WaitForComplete;

        public void Send()
        {
            switch (RefHold)
            {
                case ReferenceHold.WaitForComplete:
                    DontDestroyOnLoad(this);
                    break;
                default:
                    break;
            }

            switch (Method)
            {
                case HttpMethod.GET:
                    StartCoroutine(Get());
                    break;
                case HttpMethod.POST:
                    StartCoroutine(Post());
                    break;
            }
        }

        IEnumerator Get()
        {
            var url_ = Url;
            url_ = url_.TrimEnd('&');
            url_ = url_.TrimEnd('?');

            if (Data != null && Data != "")
            {
                if (url_.IndexOf('?') == -1)
                {
                    url_ += "?";
                    url_ += Data;
                }
                else
                {
                    url_ = url_.TrimEnd('&');
                    url_ += "&" + Data.Trim('&');
                }
            }

            UnityWebRequest www = UnityWebRequest.Get(url_);
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            www.timeout = Timeout;
            yield return www.Send();

            if (www.isNetworkError || www.isHttpError)
            {
                if (Completion != null)
                {
                    Completion(null, www.error, this);
                }
            }
            else
            {
                if (Completion != null)
                {
                    Completion(www.downloadHandler.text, null, this);
                }
            }

            Destroy(this.gameObject);
        }

        IEnumerator Post()
        {
            UnityWebRequest www = UnityWebRequest.Post(Url, Data);
            www.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            www.timeout = Timeout;
            yield return www.Send();

            if (www.isNetworkError || www.isHttpError)
            {
                if (Completion != null)
                {
                    Completion(null, www.error, this);
                }
            }
            else
            {
                if (Completion != null)
                {
                    Completion(www.downloadHandler.text, null, this);
                }
            }

            Destroy(this.gameObject);
        }
    }

    public class Http
    {
        const int defaultTimeout = 10;
        const ReferenceHold defaultRefHold = ReferenceHold.WaitForComplete;
        static Http _instance = null;
        public static Http Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Http();
                }
                return _instance;
            }
        }

        Dictionary<string, HttpSender> _senders;

        Http()
        {
            _senders = new Dictionary<string, HttpSender>();
        }

        string attachSender(
            string Url_,
            string Data_,
            HttpMethod Method_,
            int Timeout_,
            ReferenceHold RefHold_,
            System.Action<string, string> Completion_)
        {
            var guid = System.Guid.NewGuid().ToString();

            GameObject go = new GameObject("HttpSender(" + Method_.ToString() + ")" + guid);
            var sender = go.AddComponent<HttpSender>();
            sender.GUID = guid;
            sender.Url = Url_;
            sender.Data = Data_;
            sender.Method = Method_;
            sender.Timeout = Timeout_;
            sender.RefHold = RefHold_;
            sender.Completion = (string result_, string errMsg_, HttpSender sender_) =>
            {
                _senders.Remove(sender_.GUID);
                if (Completion_ != null)
                {
                    Completion_(result_, errMsg_);
                }
            };
            if (!_senders.ContainsKey(guid))
            {
                _senders.Add(guid, sender);
            }
            sender.Send();

            return guid;
        }

        public static string Send(
            string Url_,
            string Data_,
            HttpMethod Method_,
            int Timeout_,
            ReferenceHold RefHold_,
            System.Action<string, string> Completion_)
        {
            return Instance.attachSender(Url_, Data_, Method_, Timeout_, RefHold_, Completion_);
        }

        public static string Get(string URL_, string Data_, System.Action<string, string> CallBack_ = null)
        {
            return Instance.attachSender(URL_, Data_, HttpMethod.GET, defaultTimeout, defaultRefHold, CallBack_);
        }

        public static string Post(string URL_, string Data_, System.Action<string, string> CallBack_ = null)
        {
            return Instance.attachSender(URL_, Data_, HttpMethod.POST, defaultTimeout, defaultRefHold, CallBack_);
        }

        public static void Cancel(string guid)
        {
            if (Instance._senders.ContainsKey(guid))
            {
                var sender = Instance._senders[guid];
                Object.Destroy(sender.gameObject);
                Instance._senders.Remove(guid);
            }
        }

        public static void CancelAll()
        {
            var keys = Instance._senders.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                var guid = keys[i];
                var sender = Instance._senders[guid];
                Object.Destroy(sender.gameObject);
                Instance._senders.Remove(guid);
            }
        }
    }
}
