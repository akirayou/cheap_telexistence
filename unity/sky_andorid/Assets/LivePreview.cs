using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Threading;

public class LivePreview : MonoBehaviour
{
    // 取得した画像を適用するマテリアル
    /// <summary>
    /// skyBoxのマテリアルを設定する。シェーダをskybox/parnoramicに設定すること
    /// </summary>
    public Material view_material;
    
    public string URL = "http://192.168.1.1:8080/?action=stream";
    private bool updateReady = false;
    /// <summary>
    /// 実行制御用(start-disable間で使う)
    /// </summary>
    private bool isRunning; 
    void Start()
    {
        HttpClientRfresh();
        sphere_texture = new Texture2D(1, 1, TextureFormat.RGBA32,false,true);
        sphere_texture.wrapMode = TextureWrapMode.Clamp;
        isRunning = true;
        Task.Run(() => RunPreviewAsync());
        //RunPreviewAsync();
    }
    private void OnDisable()
    {
        isRunning = false;
    }
    private void Update()
    {
        /*
        float rot = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick).x + OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x;
        if (0.001 < Mathf.Abs(rot))
        {
            view_material.SetFloat("_rot", view_material.GetFloat("_rot") + rot*0.5f);
        }
        */

        //memo:本来なら4フレーム間引き程度ならthetaからのデータが8fpsなので、4フレームごとの処理でframedropすることない。
        //     効果があるのはパケット詰まりの時、パケット詰まりで纏めてデータが来た時に全部処理するという事が無くなる。
        //     パケット詰まりをまともに処理し始めると、画像遅延がスゴイ事になってくる。
        byte[] tmp = jpegData; //参照はatomicなのでロック不要
        jpegData = null;
        if (!(tmp is null) && updateReady)
        {
            sphere_texture.LoadImage(tmp, true);
            if(!(view_material.mainTexture == sphere_texture))
            {
                view_material.mainTexture = sphere_texture;
            }
            updateReady = false;
        }
        else
        {
            updateReady = true;
        }
    }

    /// <summary>
    ///  Previewのjpegストリームの受信
    /// </summary>
    private async void RunPreviewAsync()
    {
        while (true)
        {
            if (!isRunning) return;
            await GetLivePreviewAsync();
        }
    }
    //m-jpeg受信はTHETA用のを再利用

    /// <summary>
    /// THETAと通信に使うhttpclinet.シャッターボタン押された時などTCP接続を個別に強制解除する必要があるので、
    /// インスタンス毎にclientをもつ
    /// </summary>
    private HttpClient client = null;
    /// <summary>
    /// skyboxマテリアルに設定するテクスチャ
    /// </summary>
    Texture2D sphere_texture = null;
    byte[] jpegData=null;

    /// <summary>
    /// httpclinetのTCPを強制再接続(シャッターボタン対策) 
    /// HTTP接続を強制的に切って接続しなおす（THETA内部のプロセスが死んでるので、別の接続に要切り替え )
    ///Connection:closeで接続してもTHETA側が切ってくれないので、クライアント側で強制切断が必要
    /// </summary>
    private void HttpClientRfresh()
    {
        if(! (client is null))client.Dispose();
        client = new HttpClient();
        client.DefaultRequestHeaders.ExpectContinue = false;
        client.Timeout = System.TimeSpan.FromMilliseconds(300);
        client.MaxResponseContentBufferSize = 128*1024; 
    }



    private async Task<int> RecvBuf(Stream response_stream,byte[] buf, int min_len,int max_len,int nowPos=0)
    {
        try
        {
            while (nowPos < min_len)
            {
                int read_byte = await response_stream.ReadAsync(buf, nowPos, max_len - nowPos);
                if (read_byte == 0)
                {
                    Debug.LogError("Connection Lost: in read jpg");
                    return -nowPos;
                }
                nowPos += read_byte;
                //Debug.Log($"read_byte {read_byte}  ===  {nowPos} / {min_len}:{max_len}");
            }
        }
        catch (System.Exception e)
        {
            //シャッター押した際にデータが途切れてそこがReadErrorになる
            HttpClientRfresh();
            Debug.LogError($"Excepton in recvBuf \n {e}");
            return -nowPos;
        }
        return nowPos;
    }
    
    /// <summary>
    /// previewのjpegストリームを読み込む
    /// </summary>
    /// <returns></returns>
    private async Task<bool> GetLivePreviewAsync()
    {
        Stream response_stream = null;
        //previewのためのコマンド実行
        try
        {
            
            var req = new HttpRequestMessage(HttpMethod.Get, URL);
            req.Headers.ConnectionClose = true;
            var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);//最初のヘッダまで読んだら残りはstreamで処理する
            if (res.StatusCode != HttpStatusCode.OK)
            {
                Debug.LogError($"ErrorResponse on getLivePreview {res.StatusCode}");
                await Task.Delay(1000);//シャッター直後にアクセスいくと断られる,ちょっと待ってリトライ
                HttpClientRfresh();
                return true;//どうせ待たされるので、ついでにカメラ設定もやりなおす
            }
            response_stream = await res.Content.ReadAsStreamAsync();
        }
        catch (System.Exception e)
        {
            //シャッター直後にアクセスいくと、タイミングによってはタイムアウトを食らう
            HttpClientRfresh();
            Debug.Log($"Fail on : {e}");
            return false;
        }


        //
        // 以下はmultipartなデータの処理 while(true)
        //

        byte[] buf = new byte[4*1024*1024];//データ受信バッファ
        const int maxHeaderSize = 512;
        const int minJpegSize = 1024;
        while (true)
        {
            if (!isRunning) break;
            //Debug.Log("start header");
            
            int len= await RecvBuf(response_stream, buf, maxHeaderSize, minJpegSize);//確実にヘッダは読み取り(min)、jpeg全部を読み取る事はないサイズ(max)を指定このバッファの領域の中にjpegスタートヘッダがある
            if (len <= 0)
            {
                Debug.LogError("Read error in header");
                return false;
            }
            int headerLen = Array.IndexOf(buf,(byte)0xff);
            if (headerLen < 0)
            {
                Debug.LogError("Too long multipart header");
                return false;
            }
            String header = System.Text.Encoding.ASCII.GetString(buf,0,headerLen);

            //await Task.Delay(10);//できるだけまとめ読めるように、ちょっとだけ待つ。(30fps以上を出すため、33ms以上は設定禁止 )
            int jpegLen = 0;
            //Content-lengthヘッダから読み込むべきバイト数GET
            {
                int ch = header.IndexOf("Content-Length:") + 15;
                int ce = header.IndexOf("\r", ch) - ch;
                if (ch < 0 || ce < 0)
                {
                    Debug.LogError("RETRY: Broken header");
                    await Task.Yield();
                    continue;
                }
                jpegLen = int.Parse(header.Substring(ch, ce));
            }
            //Debug.Log($"start jpg:{jpegLen}");
            int totalLen = jpegLen + headerLen;
            if (buf.Length < totalLen)
            {
                Debug.LogError(@"Too large jpeg {jpegn_length}");
                return false;
            }
            
            int len2 = await RecvBuf(response_stream, buf, totalLen, totalLen,len);
            if (len2 <= 0)
            {
                Debug.LogError("Read error in jpeg body");
                return false;
            }
            

            byte[] tmp = new byte[jpegLen];
            Array.Copy(buf, headerLen, tmp, 0, jpegLen);
            jpegData = tmp;//参照はatomicなのでロック不要
         
        }
        return false;
    }

}