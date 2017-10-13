using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleHttp;

public class Sample : MonoBehaviour
{

    public void ZTest(string url)
    {
        // Http.Get(url, null, (string result, string errMsg) =>
        // {
        //     Debug.Log("Get" + "\n" + errMsg + "\n" + result);
        // });

        // Http.Post(url, "showmethemoney", (string result, string errMsg) =>
        // {
        //     Debug.Log("Post" + "\n" + errMsg + "\n" + result);
        // });

        // Http.Send(url, null, HttpMethod.GET, 3, ReferenceHold.WithScene, (string result, string errMsg) =>
        // {
        //     Debug.Log("Send" + "\n" + errMsg + "\n" + result);
        // });

        var desPath = "Test/icon.png";

        Http.Download(url, desPath, HttpFileExistOption.Replace, (int current, int total, float progress) =>
        {
            Debug.Log("current: " + current + " total: " + total + " progress: " + progress);
        },
        (int total) =>
        {
            Debug.Log("total: " + total);
            Debug.Log(System.IO.Path.Combine(Application.persistentDataPath, desPath));
        });
    }
}
