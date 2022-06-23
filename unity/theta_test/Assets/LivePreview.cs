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
    // �擾�����摜��K�p����}�e���A��
    /// <summary>
    /// skyBox�̃}�e���A����ݒ肷��B�V�F�[�_��skybox/parnoramic�ɐݒ肷�邱��
    /// </summary>
    public Material view_material;

    /// <summary>
    /// ���s����p(start-disable�ԂŎg��)
    /// </summary>
    private bool isRunning; 
    void Start()
    {
        main_context = SynchronizationContext.Current;
        HttpClientRfresh();
        sphere_texture = new Texture2D(1, 1, TextureFormat.RGBA32,false,true);
        sphere_texture.wrapMode = TextureWrapMode.Clamp;
        isRunning = true;
        RunPreviewAsync();
    }
    private void OnDisable()
    {
        isRunning = false;
    }
    int frameCount = 0;
    private void Update()
    {
        float rot = OVRInput.Get(OVRInput.RawAxis2D.LThumbstick).x + OVRInput.Get(OVRInput.RawAxis2D.RThumbstick).x;
        if (0.001 < Mathf.Abs(rot))
        {
            view_material.SetFloat("_Rotation", view_material.GetFloat("_Rotation") + rot*0.5f);
        }

        frameCount++;
        if (frameCount % 4 != 0) return;//�Ԉ������Ă݂� 
        //memo:�{���Ȃ�4�t���[���Ԉ������x�Ȃ�theta����̃f�[�^��8fps�Ȃ̂ŁA4�t���[�����Ƃ̏�����framedrop���邱�ƂȂ��B
        //     ���ʂ�����̂̓p�P�b�g�l�܂�̎��A�p�P�b�g�l�܂�œZ�߂ăf�[�^���������ɑS����������Ƃ������������Ȃ�B
        //     �p�P�b�g�l�܂���܂Ƃ��ɏ������n�߂�ƁA�摜�x�����X�S�C���ɂȂ��Ă���B
        byte[] tmp = jpegData; //�Q�Ƃ�atomic�Ȃ̂Ń��b�N�s�v
        jpegData = null;
        if (!(tmp is null))
        {
            sphere_texture.LoadImage(tmp, true);
            if(!(view_material.mainTexture == sphere_texture))
            {
                view_material.mainTexture = sphere_texture;
            }

        }
    }
    /// <summary>
    /// SetTexture�����C���X���b�h�Ŏ��s���邽�߂�context
    /// </summary>
    private System.Threading.SynchronizationContext main_context = null;


    /// <summary>
    /// THETA�̉𑜓x/fps�̐ݒ�
    /// </summary>
    /// <returns></returns>
    private async Task<bool> SetupCamera() {
        if (false == await WaitIdle()) return false;
        string result=await SetProp();
        if (result is null) return false;
        Debug.Log("THETA camera setup done");
        return true;
    }
    /// <summary>
    ///  Preview��jpeg�X�g���[���̎�M
    /// </summary>
    private async void RunPreviewAsync()
    {
        if (!isRunning) return;
        //�ŏ��̃J�����Z�b�g�A�b�v�ɃR�P��Ȃ�S�����߂Ă����B
        if(false == await SetupCamera())
        {
            isRunning = false;
            return;
        }
        while (true)
        {
            bool needSetup = await GetLivePreviewAsync();
            if (!isRunning) return;
            if (needSetup) await SetupCamera();
        }
    }


    /// <summary>
    /// THETA�Ƃ̒ʐM�Ɏg��httpclinet.�V���b�^�[�{�^�������ꂽ���Ȃ�TCP�ڑ����ʂɋ�����������K�v������̂ŁA
    /// �C���X�^���X����client������
    /// </summary>
    private HttpClient client = null;
    /// <summary>
    /// skybox�}�e���A���ɐݒ肷��e�N�X�`��
    /// </summary>
    Texture2D sphere_texture = null;
    byte[] jpegData=null;

    /// <summary>
    /// httpclinet��TCP�������Đڑ�(�V���b�^�[�{�^���΍�) 
    /// HTTP�ڑ��������I�ɐ؂��Đڑ����Ȃ����iTHETA�����̃v���Z�X������ł�̂ŁA�ʂ̐ڑ��ɗv�؂�ւ� )
    ///Connection:close�Őڑ����Ă�THETA�����؂��Ă���Ȃ��̂ŁA�N���C�A���g���ŋ����ؒf���K�v
    /// </summary>
    private void HttpClientRfresh()
    {
        if(! (client is null))client.Dispose();
        client = new HttpClient();
        client.MaxResponseContentBufferSize = 10240;
        client.DefaultRequestHeaders.ExpectContinue = false;
        client.Timeout = System.TimeSpan.FromMilliseconds(1000);
        client.MaxResponseContentBufferSize = 512 * 1024; 
    }


    [System.Serializable]
    private class ThetaStatus
    {
        [System.Serializable]
        public  class State{
            public string _captureStatus;
        };
        public State state;
    }


    /// <summary>
    /// THETA API�̃R�}���h������s����
    /// </summary>
    /// <param name="URL"></param>
    /// <param name="reqString">�R�}���h�ɓn��JSON </param>
    /// <returns></returns>
    private async Task<string>  ExeSingle(string URL,string reqString)
    {
        Debug.Log($"Exec: {URL} {reqString}");
        string ret = "";
        try
        {
            HttpContent content = new StringContent(reqString, new UTF8Encoding(), "application/json");
            var res = await client.PostAsync(URL, content);
            ret = await res.Content.ReadAsStringAsync();
        }catch(System.Exception e)
        {
            HttpClientRfresh();
            Debug.LogError($"Exception in Exec {URL} {reqString}  \n{e}");
            ret = null;   
        }
        return ret;
    }
    /// <summary>
    /// �J������Ԏ擾(idle�ł��邩�̊m�F�p)
    /// </summary>
    /// <returns></returns>
    private async Task<string> GetStatus()
    {
        const string URL = "http://192.168.1.1/osc/state";
        string req_str = @"";
        string res=await ExeSingle(URL, req_str);
        return res;
    }
    /// <summary>
    /// �J������Ԃ�idle�ɂȂ�܂ő҂�������
    /// </summary>
    /// <returns></returns>
    private async Task<bool> WaitIdle()
    {
        while (true)
        {
            string res = await GetStatus();
            if (res is null) return false;
            ThetaStatus st = JsonUtility.FromJson<ThetaStatus>(res);
            Debug.Log(res);
            if (st.state._captureStatus == "idle") break;
            await Task.Delay(200);
        }
        return true;
     }
    /// <summary>
    /// �J�����̉𑜓xfps��ݒ�
    /// </summary>
    /// <returns></returns>
    private async Task<string>  SetProp()
    {
        const string URL = "http://192.168.1.1/osc/commands/execute";
        var req_str = @"{""name"":""camera.setOptions"",""parameters"":{""options"":{""previewFormat"":{ ""width"":1920,""height"":960,""framerate"":8}}}}";
        string res = await ExeSingle(URL, req_str);
        return res;
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
            //�V���b�^�[�������ۂɃf�[�^���r�؂�Ă�����ReadError�ɂȂ�
            HttpClientRfresh();
            Debug.LogError($"Excepton in recvBuf \n {e}");
            return -nowPos;
        }
        return nowPos;
    }
    
    /// <summary>
    /// preview��jpeg�X�g���[����ǂݍ���
    /// </summary>
    /// <returns></returns>
    private async Task<bool> GetLivePreviewAsync()
    {
        Stream response_stream = null;
        //preview�̂��߂̃R�}���h���s
        try
        {
            const string URL = "http://192.168.1.1/osc/commands/execute";
            const string requestString = @"{""name"":""camera.getLivePreview""}";
            var req = new HttpRequestMessage(HttpMethod.Post, URL);
            req.Content = new StringContent(requestString, new UTF8Encoding(), "application/json");
            req.Headers.ConnectionClose = true;
            var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);//�ŏ��̃w�b�_�܂œǂ񂾂�c���stream�ŏ�������
            if (res.StatusCode != HttpStatusCode.OK)
            {
                Debug.LogError($"ErrorResponse on getLivePreview {res.StatusCode}");
                await Task.Delay(1000);//�V���b�^�[����ɃA�N�Z�X�����ƒf����,������Ƒ҂��ă��g���C
                HttpClientRfresh();
                return true;//�ǂ����҂������̂ŁA���łɃJ�����ݒ�����Ȃ���
            }
            response_stream = await res.Content.ReadAsStreamAsync();
        }
        catch (System.Exception e)
        {
            //�V���b�^�[����ɃA�N�Z�X�����ƁA�^�C�~���O�ɂ���Ă̓^�C���A�E�g��H�炤
            HttpClientRfresh();
            Debug.Log($"Fail on : {e}");
            return false;
        }


        //
        // �ȉ���multipart�ȃf�[�^�̏��� while(true)
        //

        byte[] buf = new byte[1024*1024];//�f�[�^��M�o�b�t�@
        const int maxHeaderSize = 400;
        const int minJpegSize = 1024;
        while (true)
        {
            if (!isRunning) break;
            //Debug.Log("start header");
            
            int len= await RecvBuf(response_stream, buf, maxHeaderSize, minJpegSize);//�m���Ƀw�b�_�͓ǂݎ��(min)�Ajpeg�S����ǂݎ�鎖�͂Ȃ��T�C�Y(max)���w�肱�̃o�b�t�@�̗̈�̒���jpeg�X�^�[�g�w�b�_������
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

            //await Task.Delay(10);//�ł��邾���܂Ƃߓǂ߂�悤�ɁA������Ƃ����҂B(30fps�ȏ���o�����߁A33ms�ȏ�͐ݒ�֎~ )
            int jpegLen = 0;
            //Content-length�w�b�_����ǂݍ��ނׂ��o�C�g��GET
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
            jpegData = tmp;//�Q�Ƃ�atomic�Ȃ̂Ń��b�N�s�v
         
        }
        return false;
    }

}