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
    
    public string URL = "http://192.168.1.1:8080/?action=stream";
    private bool updateReady = false;
    /// <summary>
    /// ���s����p(start-disable�ԂŎg��)
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

        //memo:�{���Ȃ�4�t���[���Ԉ������x�Ȃ�theta����̃f�[�^��8fps�Ȃ̂ŁA4�t���[�����Ƃ̏�����framedrop���邱�ƂȂ��B
        //     ���ʂ�����̂̓p�P�b�g�l�܂�̎��A�p�P�b�g�l�܂�œZ�߂ăf�[�^���������ɑS����������Ƃ������������Ȃ�B
        //     �p�P�b�g�l�܂���܂Ƃ��ɏ������n�߂�ƁA�摜�x�����X�S�C���ɂȂ��Ă���B
        byte[] tmp = jpegData; //�Q�Ƃ�atomic�Ȃ̂Ń��b�N�s�v
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
    ///  Preview��jpeg�X�g���[���̎�M
    /// </summary>
    private async void RunPreviewAsync()
    {
        while (true)
        {
            if (!isRunning) return;
            await GetLivePreviewAsync();
        }
    }
    //m-jpeg��M��THETA�p�̂��ė��p

    /// <summary>
    /// THETA�ƒʐM�Ɏg��httpclinet.�V���b�^�[�{�^�������ꂽ���Ȃ�TCP�ڑ����ʂɋ�����������K�v������̂ŁA
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
            
            var req = new HttpRequestMessage(HttpMethod.Get, URL);
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

        byte[] buf = new byte[4*1024*1024];//�f�[�^��M�o�b�t�@
        const int maxHeaderSize = 512;
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