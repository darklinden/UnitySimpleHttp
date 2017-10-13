using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleHttp;

public class Sample : MonoBehaviour
{

    public UnityEngine.UI.Image _img;

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
        Http.Download(url, desPath, HttpFileExistOption.Replace, (ulong current, ulong total, float progress) =>
        {
            Debug.Log("current: " + current + " total: " + total + " progress: " + progress);
        },
        (ulong total) =>
        {
            Debug.Log("total: " + total);

            var filePath = System.IO.Path.Combine(Application.persistentDataPath, desPath);
            Debug.Log(filePath);

            var t2d = new Texture2D(2, 2);
            var b = System.IO.File.ReadAllBytes(filePath);
            t2d.LoadImage(b);

            var sp = Sprite.Create(t2d, new Rect(0, 0, t2d.width, t2d.height), new Vector2(0.5f, 0.5f));
            _img.sprite = sp;
        });
    }
}
