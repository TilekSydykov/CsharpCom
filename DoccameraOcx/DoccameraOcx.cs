﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace DoccameraOcx
{
    // [ComVisible(true), Guid("454C18E2-8B7D-43C6-8C17-B1825B49D7DE"), ClassInterface(ClassInterfaceType.None), ProgId("DoccameraOcx")]
    [Guid("454C18E2-8B7D-43C6-8C17-B1825B49D7DE")]
    // [ComDefaultInterface(typeof(IDoccameraControl))]
    [System.Security.SecuritySafeCritical] //防止 2 级透明将导致 AllowPartiallyTrustedCallers 程序集中的所有方法都变成安全透明的，这可能是导致发生此异常的原因。
    public partial class DoccameraOcx : BaseActiveX
    {
        #region 公共变量
        //    AxCmCaptureOcxLib.AxCmCaptureOcx camCaptureOcx = null;//定义全局高拍仪控件变量
        FaceVerifyWrapper mFaceVerify = new FaceVerifyWrapper();//人脸识别基类
        int lInitOCX = -999;//初始化OCX结果
        int iMainDevNo = 0;//主摄像头序号
        int iDeputyDevNo = 0;//副摄像头序号
        int isNoZGCamera = 0;//是否检测到紫光高拍仪
        int iDevNo;//设备数量
        int iWaterfontSize = 8;//水印字体
        uint lWaterRGB = 0;//水印颜色
        int iWaterX = 0;//x坐标
        int iWaterY = 0;//y坐标
        int iCount = 0;//设备总数量
        int iDevType;//设备类型 0 主摄像头 1 usb摄像头
        string strPath;//当前dll路径
        string sdn_dual = "0";//是否具有双目活体检测功能 0：无 1：有
        string sdn_verify = "0";//是否具有人证比对功能 0：无 1：有
        string sdn_readCardType = "0";//获取身份证信息方式 0使用六合一读卡方式  1 内部控制读卡
        public byte[] CapturedPackage = null; //识别成功后抓拍到的图片数据
                                              //   public byte[] DBImage = null;//身份证头像招聘
        string strFaceImg;//双目摄像头抓拍到的人脸图片
        string strIdentifyNo;//身份证号
        string strCardName;//身份证上姓名
        string strPhotoBase;//身份证头像Base64 字符串
        string strVerifyScore = "0";//人证比对结果1 通过 2未通过
        #endregion

        #region 析构与构造函数
        public DoccameraOcx()
        {
            InitializeComponent();
            //实例化AxtiveX控件
            //camCaptureOcx = new AxCmCaptureOcxLib.AxCmCaptureOcx();
            // Application.StartupPath
            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);
            strPath = Path.GetDirectoryName(path);

        }
        /// <summary>
        /// 析构函数
        /// </summary>
        ~DoccameraOcx()
        {
            try
            {
                // MessageBox.Show("执行析构函数");
                bStopPlay();
            }
            catch { }
        }
        #endregion

        #region OCX对外函数
        /// <summary>
        /// 根据指定类型切换显示内容
        /// </summary>
        /// <param name="iType">1:高拍仪 2：双目摄像头</param>
        private void sdn_ChangeShow(int iType)
        {
            try
            {
                strVerifyScore = "0"; //初始化人证比对成绩
                if (sdn_dual == "1")
                {
                    if (iType == 1) //高拍仪
                    {
                        sdnDual.Visible = false;
                        sdnZGOcx.Visible = true;
                        sdnZGOcx.Width = panel1.Width;
                        sdnZGOcx.Height = panel1.Height;
                        sdnZGOcx.Top = panel1.Top;
                        sdnZGOcx.Left = panel1.Left;
                        plShowMsg.Visible = false;//显示结果不可见
                        lbmsg.Visible = false;//双目摄像头活体检测结果 不可见
                        btnAgain.Visible = false;//比对按钮不可见

                    }
                    else if (iType == 2) //双目摄像头
                    {
                        sdnZGOcx.Visible = false;
                        sdnDual.Visible = true;
                        sdnDual.Width = panel1.Width;
                        sdnDual.Height = panel1.Height - 55;
                        sdnDual.Top = panel1.Top;
                        sdnDual.Left = panel1.Left + 100;
                        plShowMsg.Visible = true;//显示结果不可见
                        lbmsg.Visible = true;//双目摄像头活体检测结果 不可见
                        btnAgain.Visible = false;//比对按钮不可见
                    }
                }
                strFaceImg = "";//清空缓存
                CapturedPackage = null;
            }
            catch { }
        }
        /// <summary>
        /// 初始化OCX
        /// </summary>
        private void InitOcx()
        {

        }
        /// <summary>
        /// 得到设备序号
        /// </summary>
        private void GetDevNo()
        {
            //1  初始化OCX
            if (lInitOCX == -999) //如果ocx未初始化或者初始化未成功
            {
                lInitOCX = sdnZGOcx.Initial(); //初始化OCX
            }
            if (lInitOCX == -2) //无设备
            {
                iDevNo = 0;//无设备
                isNoZGCamera = 0;
                return;
            }
            else if (lInitOCX == -1) //无授权设备
            {
                isNoZGCamera = 0;
            }
            //2 获取设备数量
            iCount = sdnZGOcx.GetDevCount();
            iDevNo = iCount;
            if (iCount > 0)
            { //如果设备数量多于1
                for (int i = 0; i < iCount; i++)
                {//循环遍历设备，根据设备名称找到高拍仪设备
                    string devName = sdnZGOcx.GetDevFriendName(i); //得到设备名称
                    if (devName == "338 Camera") //找到紫光高拍仪
                    {
                        isNoZGCamera = 1; //具有紫光高拍仪
                        iMainDevNo = i; //紫光高拍仪为主摄像头
                    }
                    else
                    {
                        iDeputyDevNo = i; //非紫光高排仪器为副摄像头
                    }
                }
            }
            else
            { //没有设备
                iDevNo = 0;//无设备
            }
        }
        /// <summary>
        /// 修改手动剪裁 边框xml配置
        /// </summary>
        /// <param name="iX1"></param>
        /// <param name="iY1"></param>
        /// <param name="iX2"></param>
        /// <param name="iY2"></param>
        /// <returns></returns>
        private bool sdnModifyXML(int iX1, int iY1, int iX2, int iY2)
        {
            return true;
        }
        /// <summary>
        /// 显示主摄像头视频源
        /// </summary>
        /// <returns>TRUE——成功 FALSE——失败</returns>
        public bool bStartPlay()
        {
            sdn_ChangeShow(1);//显示高拍仪内容
            iDevType = 0;//当前开启主摄像头
            GetDevNo();//初始化设备参数
            if (iDevNo == 0) //如果没有设备
            {
                //sdn_ZGOCX.StartRun(iMainDevNo);
                return false;
            }
            else if (isNoZGCamera == 0)//或者不存在紫光高拍仪设备
            {
                sdnZGOcx.StartRun(0); //打开默认摄像头
                return false;
            }
            sdnZGOcx.StartRun(iMainDevNo);
            return true;
        }
        /// <summary>
        /// 停止视频源
        /// </summary>
        /// <returns></returns>
        public bool bStopPlay()
        {
            sdn_ChangeShow(1);//显示高拍仪内容
            strVerifyScore = "0"; //初始化人证比对成绩
            GetDevNo();//初始化设备参数
            if (iDevNo == 0)
            {
                return false;
            }
            sdnZGOcx.Destory();
            lInitOCX = -999;
            try
            {//默认打开第一个tab框
                if (sdn_dual == "1") //具有双目活体检测
                {
                    sdnZGOcx.Visible = false;//高拍仪控件不可见
                    sdnDual.Visible = false;
                    plShowMsg.Visible = false;
                    lbmsg.Visible = false;
                    sdnDual.StopLiveness();
                    sdnDual.CloseCamera();
                    sdnDual.UnInitialize();
                }
            }
            catch { }
            return true;
        }
        /// <summary>
        /// 显示副视频源
        /// </summary>
        /// <param name="sRotate">旋转角度</param>
        /// <returns></returns>
        public bool bStartPlay2(short sRotate)
        {
            iDevType = 1;//当前开启副摄像头
            try
            {//打开第二个tab框
                if (sdn_dual == "1") //具有双目活体检测
                {
                    strVerifyScore = "0"; //初始化人证比对成绩
                    sdn_ChangeShow(2);//显示双目摄像头
                    sdnDual.Initialize();//初始化比对
                    sdnDual.OpenCamera();//打开摄像头
                    sdnDual.StartLiveness();//开始活检
                    return true;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            GetDevNo();//初始化设备参数
            short iRotate;//视频旋转角度
            switch (sRotate)
            {
                case 0:
                    iRotate = 0;
                    break;
                case 90:
                    iRotate = 1;
                    break;
                case 180:
                    iRotate = 2;
                    break;
                case 270:
                    iRotate = 3;
                    break;
                default:
                    iRotate = 0;
                    break;
            }
            if (iDevNo == 0) //如果没有设备
            {
                //sdn_ZGOCX.StartRun(iMainDevNo);
                return false;
            }
            else if (isNoZGCamera == 0)//或者不存在紫光高拍仪设备
            {
                sdnZGOcx.RotateVideo(iRotate);
                sdnZGOcx.StartRun(0); //打开默认摄像头
                return true;
            }
            sdnZGOcx.RotateVideo(iRotate);
            sdnZGOcx.StartRun(iDeputyDevNo);
            return true;
        }
        /// <summary>
        /// 带角度的显示主摄像头视频源
        /// </summary>
        /// <param name="rotate">旋转角度</param>
        /// <returns></returns>
        public bool bStartPlayRotate(short rotate)
        {
            sdn_ChangeShow(1);//显示高拍仪内容
            iDevType = 0;//当前开启主摄像头
            GetDevNo();//初始化设备参数
            short iRotate;//视频旋转角度
            switch (rotate)
            {
                case 0:
                    iRotate = 0;
                    break;
                case 90:
                    iRotate = 1;
                    break;
                case 180:
                    iRotate = 2;
                    break;
                case 270:
                    iRotate = 3;
                    break;
                default:
                    iRotate = 0;
                    break;
            }
            if (iDevNo == 0) //如果没有设备
            {
                //sdn_ZGOCX.StartRun(iMainDevNo);
                return false;
            }
            else if (isNoZGCamera == 0)//或者不存在紫光高拍仪设备
            {
                sdnZGOcx.RotateVideo(iRotate);
                sdnZGOcx.StartRun(0); //打开默认摄像头
                return false;
            }
            sdnZGOcx.RotateVideo(iRotate);
            sdnZGOcx.StartRun(iMainDevNo);
            return false;
        }
        /// <summary>
        /// 显示并设置视频源参数
        /// </summary>
        public void displayVideoPara()
        {

        }
        /// <summary>
        /// 设置PIN参数
        /// </summary>
        public void vSetCapturePin()
        {

        }
        /// <summary>
        /// 设置保存的图片宽高分辨率的缩放率
        /// </summary>
        /// <param name="fImageXYZoom">fImageXYZoom——宽高分辨率的缩放率（默认值为 1.0）</param>
        /// <returns></returns>
        public bool bSetIamgeXYZoom(float fImageXYZoom)
        {
            return true;
        }
        /// <summary>
        /// 设置拍照区域大小（宽和高分为 10000 份））
        /// </summary>
        /// <param name="iX1">iX1——拍照区域的左边 Left（1－10000）</param>
        /// <param name="iY1">iY1——拍照区域的上边 Top（1－10000）</param>
        /// <param name="iX2">iX2——拍照区域的右边 Right（1－10000）</param>
        /// <param name="iY2">iY2——拍照区域的下边 Bottom（1－10000）</param>
        /// <returns></returns>
        public bool bSetImageArea(short iX1, short iY1, short iX2, short iY2)
        {
            if (lInitOCX == -999) //如果ocx未初始化或者初始化未成功
            {
                lInitOCX = sdnZGOcx.Initial(); //初始化OCX
            }
            //对应紫光高拍仪操作
            //1 先设置为手动剪裁模式
            if (sdnZGOcx.CusCrop(1) == 1)
            {
                //2 设置xml文件中剪裁的区域

                //3 加载对应的剪裁xml参数
                sdnZGOcx.SetCusCropType(2); //加载type为2的紫光高拍仪剪裁模式
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 获取最大曝光值
        /// </summary>
        /// <returns> 最大曝光值 0 - 20</returns>
        public int lGetExposureMax()
        {
            if (lInitOCX == -999)
            {
                lInitOCX = sdnZGOcx.Initial();//初始化OCX
            }
            string strExpValue = sdnZGOcx.GetExposureRange();
            string[] arrStr = strExpValue.Split('|');
            if (arrStr.Length > 0)
            {
                string maxExpValue = arrStr[1];
                // long Lmax = long.TryParse(maxExpValue);
                return Convert.ToInt32(maxExpValue);
            }
            return 0;
        }
        /// <summary>
        /// 获取最小曝光值
        /// </summary>
        /// <returns>最小曝光值 0-20</returns>
        public int lGetExposureMin()
        {
            if (lInitOCX == -999)
            {
                lInitOCX = sdnZGOcx.Initial();//初始化OCX
            }
            string strExpValue = sdnZGOcx.GetExposureRange();
            string[] arrStr = strExpValue.Split('|');
            if (arrStr.Length > 0)
            {
                string minExpValue = arrStr[0];
                // long Lmax = long.TryParse(maxExpValue);
                return Convert.ToInt32(minExpValue);
            }
            return 0;
        }
        /// <summary>
        /// 获取当前曝光值
        /// </summary>
        /// <returns>当前曝光值 0-20</returns>
        public int lGetExposure()
        {
            return 15;
        }
        /// <summary>
        /// 设置曝光值
        /// </summary>
        /// <param name="iExposure">曝光值</param>
        public void vSetExposure(int iExposure)
        {
            int lExp = -1;
            int sV = (iExposure / 4); //0 1 2 3 4 5
            switch (sV)
            {
                case 0:
                    lExp = -6;
                    break;
                case 1:
                    lExp = -5;
                    break;
                case 2:
                    lExp = -4;
                    break;
                case 3:
                    lExp = -3;
                    break;
                case 4:
                    lExp = -2;
                    break;
                case 5:
                    lExp = -1;
                    break;
                default:
                    lExp = -1;
                    break;
            }
            sdnZGOcx.SetExposure(lExp);
        }
        /***********************************************************************
*函数名称：vSetResolutionEx
* 功能描述：设置分辨率
* 输入参数：devIndex——摄像头索引，１，主摄像头，２副摄像头
resolutionType——分辨率类型
1—320 * 240
2—640 * 480
3—800 * 600
4—1024 * 768
5—1600 * 1200
6—2048 * 1536
7—2592 * 1944
0 / 其它—按设备默认值分辨率
* 输出参数：无
* 返回值： 无
****************************************************************/
        public void vSetResolutionEx(short devIndex, short resolutionType)
        {
            sdnZGOcx.SetResolution(resolutionType);
        }
        /************************************************************************
*函数名称：bSetMode
* 功能描述：设置拍照模式
* 输入参数：iMode——拍照模式
0—支持鼠标框选模式（默认模式） 摄像头1、2、3
1—定义固定大小拍照模式 		摄像头1、2、3
2—定义固定大小身份证拍照模式 	摄像头1
3—自动寻边 					摄像头1
4—自动寻边身份证拍照模式 自动裁切，先拍摄正面在拍摄反面然后上下合并
摄像头1
5—多图裁切						摄像头1
6—人脸裁切						摄像头2、3
* 输出参数：无
* 返回值： TRUE——成功 FALSE——失败
* 例如： m_cap.bSetMode(0);//设置为默认鼠标可以框选的模式
*六合一用到 0 3 4 3
****************************************************************/
        public bool bSetMode(short iMode)
        {
            return true; //效果不好，去掉，不自动裁切，单氐楠  2018年8月28日 高速一大队

            if (iMode == 3) //自动巡边剪裁
            {
                long lRes = sdnZGOcx.AutoCrop(1);
                if (lRes > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
        /// <summary>
        /// 保存为JPG图片
        /// </summary>
        /// <param name="filePath">保存图片的路径</param>
        /// <param name="fileName">保存图片的名称（不用包含后缀名）</param>
        /// <returns></returns>
        public bool bSaveJPG(string filePath, string fileName)
        {
            string imgPath; //图片地址
            string cstrFilePath = filePath;
            //cstrFilePath.ReverseFind('\\');
            if (cstrFilePath.LastIndexOf("\\") > 0)   //分析最后一位
            {
            }

            else
            {
                cstrFilePath = cstrFilePath + "\\";
            }
            imgPath = cstrFilePath + fileName + ".jpg";
            // 返回值 1：拍图成功 0：未找到图片 - 1：视频流为空 - 2：文件名为空 - 3：无图像 - 4：存图失败
            //设置图片格式
            //文件格式编号0、bmp；1、jpg；2、tif；3、png; 4、gif
            if (sdnZGOcx.SetFileType(1) == 1)
            {
            }
            int LCapture = sdnZGOcx.CaptureImage(imgPath);
            if (LCapture == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// 获取指定图像类型文件的 Base64 编码数据
        /// </summary>
        /// <param name="imageType">imageType——图像类型</param>
        /// 1—BMP 2—GIF 3—JPG 4—PNG 5—TIF（黑白） 其它—JPG
        /// <returns>返回 Base64 编码后的数据</returns>
        public string sGetImageBase64(int imageType)
        {
            //文件格式编号0、bmp；1、jpg；2、tif；3、png; 4、gif
            if (sdnZGOcx.SetFileType(imageType) == 1) //设置图片格式成功
            {
                //图片颜色格式0彩色1灰度2黑白3印章4签名（PNG）
                if (imageType == 5)
                    sdnZGOcx.SetImageColorMode(2); //灰色

                return sdnZGOcx.CaptureToBase64();
            }
            else
            {
                return null;
            }
        }
        /************************************************************************
*函数名称：bSaveMergeStart
* 功能描述：开始合成图片
* 输入参数：filePath——保存图片的路径
fileName——保存图片的名称（不用包含后缀名）
fileType——图片类型, 0 tif 1表示jpg 其他jpg
cols——横向列数，如横向为 2，则先排完 2 张后再折行
olGap——横向间隔距离
rowGap——纵向间隔距离
* 输出参数：无
* 返回值： TRUE——成功 FALSE——失败
****************************************************************/
        public bool bSaveMergeStart(string filePath, string fileName, int fileType, int cols, int colGap, int rowGap)
        {
            return true;
        }
        /************************************************************************
*函数名称：bSaveMergePage
* 功能描述：保存需合并的单页
* 输入参数：filePath——保存图片的路径
* 输出参数：无
* 返回值： TRUE——成功 FALSE——失败
****************************************************************/
        public bool bSaveMergePage()
        {
            return true;
        }
        /***********************************************************************
*函数名称：bSaveMergeEnd
* 功能描述：结束合成图片
* 输入参数：无
* 输出参数：无
* 返回值： TRUE——成功 FALSE——失败
****************************************************************/
        public bool bSaveMergeEnd()
        {
            return true;
        }
        /***********************************************************************
*函数名称： bUpLoadImageEx
* 功能描述： 上传指定图片到服务器
* 输入参数： fileName——上传图片的完整路径（多文件上传时， 用“ | ” 隔开） serverName——服务器地址（IP、 域名） usPort——端口号
objectName——处理图片上传的服务器对象 （文件请求字段名称：trackdata）
bWaitUI——是否显示等待界面
bRetUI——是否显示结果界面

****************************************************************/
        public bool bUpLoadImageEx(string fileName, string serverName, int usPort, string objectName, bool bWaitUI, bool bRetUI)
        {
            string[] arrFileName = fileName.Split('|');
            foreach (string str in arrFileName)
            {
                string cstrServiceUrl = "http://";
                cstrServiceUrl += serverName;
                cstrServiceUrl += ":";
                cstrServiceUrl += usPort;
                cstrServiceUrl += "/";
                cstrServiceUrl += objectName;
                //参数一：字符串  URL地址  参数二：文件名（包含路径）参数三 true：上传后删除本地文件
                sdnZGOcx.UpdataFile(cstrServiceUrl, str, 1);
            }

            return true;
        }
        /***********************************************************************
*函数名称：sUpLoadImageEx2
* 功能描述：上传指定文件到服务器，并返回服务端响应内容
* 输入参数：fileName——上传文件的完整路径
serverName——服务器地址（IP、域名）
usPort——端口号
objectName——处理图片上传的服务器对象 （文件请求字段名称：trackdata）
bWaitUI——是否显示等待界面
bRetUI——是否显示结果界面
* 输出参数：无
* 返回值： 服务器响应内容
****************************************************************/
        public string sUpLoadImageEx2(string fileName, string serverName, int usPort, string objectName, bool bWaitUI, bool bRetUI)
        {
            string cstrResponse = "";
            //输入参数：filePath 要上传的文件路径；serverIP 服务IP；serverPort 服务端口 requestPath 请求页面地址 isDelFile 是否删除本地文件
            //sdnHttpUploadImgs.SendTrack(fileName, serverName, usPort, objectName, cstrResponse, true);
            //  GetIndentifyMsg();
            return cstrResponse;//上传文件并得到返回值
        }
        /// <summary>
        /// 删除文件或目录（删除到回收站）
        /// </summary>
        /// <param name="pathName"></param>
        /// <returns></returns>
        public bool bDeleteFile(string pathName)
        {
            try
            {
                if (File.Exists(pathName))
                {
                    File.Delete(pathName);
                }
                if (Directory.Exists(pathName))
                {
                    Directory.Delete(pathName);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 删除文件（永久删除）
        /// </summary>
        /// <param name="pathName"></param>
        /// <returns></returns>
        public bool bDeleteFileForever(string pathName)
        {

            try
            {
                if (File.Exists(pathName))
                {
                    File.Delete(pathName);
                }
                if (Directory.Exists(pathName))
                {
                    Directory.Delete(pathName);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 创建目录
        /// </summary>
        /// <param name="dirPath">目录路径</param>
        /// <returns>true 成功 false失败</returns>
        public bool bCreateDir(string dirPath)
        {
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);
            return true;
        }
        /// <summary>
        /// 判断目录是否存在
        /// </summary>
        /// <param name="dirPath">目录路径</param>
        /// <returns></returns>
        public bool bDirIsExist(string dirPath)
        {
            if (Directory.Exists(dirPath))
                return true;
            return false;
        }
        /// <summary>
        /// 设置水印参数信息
        /// </summary>
        /// <param name="iWaterPrintFlag">水印开关（ 0： 不打水印， 1： 打水印）</param>
        /// <param name="sWaterPrintInfo">水印内容</param>
        /// <param name="iAddTimeFlag">水印内容加时间（ 0： 不加， 1： 加）</param>
        public void vSetWaterPrint(int iWaterPrintFlag, string sWaterPrintInfo, int iAddTimeFlag)
        {
            string strWaterInfo;
            if (iWaterPrintFlag == 1)
            {
                strWaterInfo = sWaterPrintInfo;
                //sdn_ZGOCX.SetMarkString(0, 45, sWaterPrintInfo);
                if (iAddTimeFlag == 1)
                {
                    DateTime dtNow = DateTime.Now;
                    //以上为获取系统时间
                    //sdn_ZGOCX.SetTimeMark((strDate + " " + strTime).GetBuffer(), 45);
                    strWaterInfo += (" " + dtNow.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                //SetMarkStringEx 参数如下
                //LONG型  角度（取值范围 - 180~181，取181时代表倾斜度为对角线角度）
                //LONG型  字体大小
                //BSTR 型 水印文字
                //DWORD型 颜色
                //LONG型 字体
                //LONG型 x轴偏移
                //LONG型 y轴偏移
                //LONG型 排版格式
                sdnZGOcx.SetMarkStringEx(0, iWaterfontSize, strWaterInfo, lWaterRGB, 1, iWaterX, iWaterY, 0);
            }
        }
        /// <summary>
        /// 设置水印字体大小
        /// </summary>
        /// <param name="fontSize">水印字体大小</param>
        public void vSetWaterPrintFontSize(int fontSize)
        {
            iWaterfontSize = fontSize;
        }
        /// <summary>
        /// 设置水印字体颜色
        /// </summary>
        /// <param name="rgb">水印字体颜色（0x000000 - 0xFFFFFF）</param>
        public void vSetWaterPrintFontColor(int rgb)
        {
            lWaterRGB = (uint)rgb;
        }
        /// <summary>
        /// 设置水印位置
        /// </summary>
        /// <param name="x">设置水印位置x 轴</param>
        /// <param name="y">设置水印位置 y 轴</param>
        public void vSetWaterPrintPos(int x, int y)
        {
            iWaterX = x;
            iWaterY = y;
        }
        /// <summary>
        /// 设置图片保存的压缩率 （拍.BMP 图片本函数无效）
        /// </summary>
        /// <param name="sImageQuality">图片保存的压缩率（区间：1－100，默认值：70）</param>
        public void vSetImageQuality(int sImageQuality)
        {
            sdnZGOcx.SetJpgQuanlity(sImageQuality);
        }
        /// <summary>
        /// 上传指定图片到服务器
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="serverName"></param>
        /// <param name="usPort"></param>
        /// <param name="objectName"></param>
        /// <returns></returns>
        public bool bUpLoadImage(string fileName, string serverName, int usPort, string objectName)
        {

            return true;
        }
        /// <summary>
        /// 得到base64编码的图片
        /// </summary>
        /// <returns></returns>
        public string sGetBase64()
        {
            try
            {
                if (sdn_dual == "1" && iDevType == 1) //如果具有双目摄像头 活体检测功能 且当前操作的为双目摄像头
                {
                    if (!string.IsNullOrWhiteSpace(strFaceImg)) //如果当前检测数据不为空，即检测到数据
                    {
                        return strFaceImg;
                    }
                    else
                    {
                        DialogResult dr = MessageBox.Show("活检未完成,请等待完成", "警告", MessageBoxButtons.OK);
                        if (dr == DialogResult.OK)
                        {
                            return strFaceImg;
                        }
                        else
                        {
                            return strFaceImg;
                        }

                    }
                }
                if (sdn_verify == "1" && iDevType == 1) //具有人证比对功能且当前为双目
                {
                    if (strVerifyScore == "0") //人证比对未通过
                    {
                        DialogResult dr = MessageBox.Show("人证比对未通过，请等待", "警告", MessageBoxButtons.OK);
                        if (dr == DialogResult.OK)
                        {
                            return "";
                        }
                        else
                        {
                            return "";
                        }
                    }
                }
            }
            catch { }
            if (iDevType == 0)
            { //主摄像头 证件照 只压缩高拍仪不压缩USB摄像头
                vSetImageQuality(60);//压缩图片，越大压缩越小 质量越好
            }
            string strBaseImg = sGetImageBase64(1);
            try
            {
                new Thread(saveUploadJpg).Start(strBaseImg);
            }
            catch { }
            return strBaseImg;
        }
        /// <summary>
        /// 保存并上传图片
        /// </summary>
        private void saveUploadJpg(object objImg)
        {
            try
            {
                string strBaseImg = objImg.ToString();
                byte[] byteImg = Convert.FromBase64String(strBaseImg);
                ReadIniFile readIni = new ReadIniFile(strPath + "\\sdnsystem.ini");
                string strIP = readIni.ReadValue("uploadimgs", "ip");
                string strPort = readIni.ReadValue("uploadimgs", "port");
                string strAdd = readIni.ReadValue("uploadimgs", "requestpath");
                string strAddress = "http://" + strIP + ":" + strPort + strAdd;
                string strFilePath;
                //  MessageBox.Show(strAddress);
                if (iDevType == 0)
                { //主摄像头 证件照
                    strFilePath = "2_sdn1234555.jpg";
                }
                else
                { //人员照片
                    strFilePath = "1_sdn1234555.jpg";
                }
                new sdnHttpUploadFIle().Upload_Request(strAddress, byteImg, strFilePath);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        #endregion

        #region 双目摄像头

        private void sdnDual_sdnOnCaptureStatus(object sender, AxsdnDualCameraLivenessLib._DsdnDualCameraLivenessEvents_sdnOnCaptureStatusEvent e)
        {
            // MessageBox.Show(e.lParam + "");
            string cameraTips = "";
            Thread.Sleep(10);
            if (e.wParam == 1)
            {
                cameraTips = "捕获成功";
            }
            else
            {
                cameraTips = this.GetTipDualCamera(e.lParam);
            }
            lbmsg.Visible = true;
            this.lbmsg.Text = cameraTips;
        }
        /// <summary>
        /// 识别成功
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sdnDual_OnCaptureSuccessCallbackHandler(object sender, EventArgs e)
        {
            MessageBox.Show("捕获成功");
            CapturedPackage = Convert.FromBase64String(this.sdnDual.livenessVerificationPackage);
            strFaceImg = this.sdnDual.capturedFaceImageContent;
            //VisiblePhoto.Image = Bitmap.FromStream(new MemoryStream(byteFaceImage));
            // InfredPhoto.Image = Bitmap.FromStream(new MemoryStream(byteInfredFaceImage));
            //   MessageBox.Show("捕获成功");
            this.lbmsg.Text = "捕获成功";
            this.sdnDual.StopLiveness(); //停止活检
            this.sdnDual.CloseCamera(); //关闭摄像头
            if (sdn_verify == "1") //具有前端人脸识别功能
            {
                sdn_Verify();//活体检测成功后自动识别比对
            }
        }
        /// <summary>
        /// 活体检测显示内容
        /// </summary>
        /// <param name="FrameStatus"></param>
        /// <returns></returns>
        private string GetTipDualCamera(int FrameStatus)
        {
            string strRet = "请正对摄像头,并除去遮挡物";
            switch (FrameStatus)
            {
                case 1:
                    strRet = "请正对摄像头,并除去遮挡物";
                    break;
                case 2:
                    strRet = "请保持机器前中只有一人";
                    break;
                case 300:
                    strRet = "请注视摄像头";
                    break;
                case 401:
                    strRet = "请稍许靠后站立";
                    break;
                case 402:
                    strRet = "请稍许靠前站立";
                    break;
                case 501:
                    strRet = "请摘下眼镜";
                    break;
                case 502:
                    strRet = "请摘下口罩";
                    break;
                case 503:
                    strRet = "请摘下眼镜";
                    break;
                case 600:
                    strRet = "请保持严肃";
                    break;
                case 700:
                    strRet = "请维持人脸静止，不要晃动";
                    break;

                case 0:
                    strRet = "未知问题，请调整摄像头";
                    break;
                default:
                    break;
            }

            return strRet;

        }


        #endregion

        #region 前端人证比对
        /// <summary>
        /// 前端人证比对系统
        /// </summary>
        private void sdn_Verify()
        {
            GetIndentifyMsg();//读取身份证信息
            Directory.SetCurrentDirectory(strPath); //设置当前文件路径
            byte[] DBImage = null;
            if (string.IsNullOrWhiteSpace(strPhotoBase) && !strPhotoBase.Contains(','))
            {
                DialogResult dr = MessageBox.Show("处理身份证信息失败，请把身份证放到读卡器上再次读卡!", "警告", MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK)
                {
                    sdn_Verify();
                }
                return;
            }
            try
            {
                DBImage = Convert.FromBase64String(strPhotoBase.Split(',')[1]);//根据身份证头像路径 得到对应的图片字节数组
                int rtn = 0;
                rtn = mFaceVerify.FaceVerifyInit(".", 2);
                if (rtn == FaceVerifyWrapper.RTN_LICENSE_ERROR)
                {
                    MessageBox.Show("请申请license！", "人脸比对SDK");
                    return;
                }
                else if (rtn != FaceVerifyWrapper.RTN_SUCC)
                {
                    MessageBox.Show("初始化失败！", "人脸比对SDK");
                    return;
                }

                //以上为人脸识别

                mFaceVerify.ChangeQueryPackage(CapturedPackage); //
                mFaceVerify.ChangeDBImage(DBImage, 1);
                double compareNum = 0;
                int tempNum = this.mFaceVerify.CompareDBQuery(ref compareNum);
                //this.tempNum = this.mFaceVerify.ComparePersonPair(,DBImage ref compareNum);
                int i = 0;
                //  MessageBox.Show(compareNum+"");
                double iMinValue = Convert.ToDouble(new ReadIniFile(strPath + @"\sdnsystem.ini").ReadValue("FaceConfig", "faceconfig"));
                if (compareNum > (iMinValue < 60 ? 60 : iMinValue))
                {
                    this.lbmsg.ForeColor = Color.Green;//是同一个人显示绿色
                    this.lbmsg.Text = "比对通过";
                    i = 1;
                    strVerifyScore = "1";
                }
                else
                {
                    this.lbmsg.ForeColor = Color.Red;
                    this.lbmsg.Text = "比对失败,请核准是否为本人";
                    i = 0;
                    strVerifyScore = "0";
                    btnAgain.Visible = true;//比对按钮可见
                }
                try
                {
                    mFaceVerify.FaceVerifyUninit();
                }
                catch
                {
                }
                // MessageBox.Show("222"+strRes);
            }
            catch (Exception ex)
            {
                MessageBox.Show("人证比对失败!" + ex.Message);
            }

        }

        /// <summary>
        /// 从本地文件获取图片字节数组
        /// </summary>
        private byte[] GetByteImgFromFile(string strImgPath)
        {

            if (!string.IsNullOrWhiteSpace(strImgPath)) //判断是否为空
            {
                //string fName = strImgPath;
                //Bitmap bmp = new Bitmap(fName);

                //DBImage = ms.GetBuffer();
                //ms.Close();
                return File.ReadAllBytes(strImgPath);
            }
            else
            {
                return null;
            }


        }

        #endregion

        #region 获取身份证信息
        /// <summary>
        /// 使用六合一插件获取读卡信息
        /// </summary>
        private void GetIndentifyMsg()
        {
            try
            {
                if (sdn_readCardType == "0")
                {
                    #region 使用六合一插件直接读取

                    string strIdentify = axPrinter1.GetQrText();//得到身份证信息
                    if (string.IsNullOrWhiteSpace(strIdentify))//如果读取身份证信息未空
                    {
                        DialogResult dr = MessageBox.Show("读取身份证信息失败，请把身份证放到读卡器上再次读卡!", "警告", MessageBoxButtons.OK);
                        if (dr == DialogResult.OK)
                        {
                            GetIndentifyMsg();
                            return;
                        }
                    }
                    string[] arrIdentify = strIdentify.Split('|'); //使用 | 分割读卡数据
                    strCardName = arrIdentify[1];//身份证姓名
                    strIdentifyNo = arrIdentify[6]; //身份证号码
                    strPhotoBase = arrIdentify[11];//身份证头像路径

                    #endregion
                }
                else
                {
                    #region 通过process 关闭六合一循环读卡进程获取身份证信息
                    //1 关闭已经打开的process进程
                    string strFilePath = sdn_CloseCard();//关闭六合一 监控程序  进程
                                                         //2 读卡获取身份证信息
                    IdentityCard sdnReadCard = new IdentityCard();
                    string strResMsg = "";
                    IDCard sdnIdMsg = sdnReadCard.GetBaseMsg(1, out strResMsg);
                    strCardName = sdnIdMsg.Name;
                    strIdentifyNo = sdnIdMsg.CartNo;
                    strPhotoBase = sdnIdMsg.Photo;//身份证头像路径
                                                  //3 重新打开process进程
                    sdn_openCard(strFilePath);

                    #endregion
                }
                
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        /// <summary>
        /// 关闭六合一读卡器进程
        /// </summary>
        private string sdn_CloseCard()
        {
            try
            {
                string strFilePath = "";//进程文件路径
                                        //  Process[] plist = Process.GetProcesses();//得到该计算机上的所有进程
                Process[] plist = Process.GetProcessesByName("监控程序"); //获取监控程序路径
                Process sdnProcess = null;
                foreach (Process p in plist)
                {
                    //    if(p.ProcessName=="")
                    //    {
                    sdnProcess = p;
                    strFilePath = p.MainModule.FileName.ToString();//得到该进程的完成路径
                    // }
                }

                if (sdnProcess != null)
                {
                    sdnProcess.Close();
                    sdnProcess.Kill();

                }
                return strFilePath;//返回该进程的完整路径
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// 根据文件全路径打开某个文件
        /// </summary>
        /// <param name="strFilePath"></param>
        private void sdn_openCard(string strFilePath)
        {
            try
            {
                Process P = new Process();
                P.StartInfo.UseShellExecute = false;  //这条属性必须设置为false，否则无法以用户模式运行程序
                P.StartInfo.RedirectStandardError = true;  //以下3条属性MSDN上有详解
                P.StartInfo.RedirectStandardInput = true;
                P.StartInfo.RedirectStandardOutput = true;
                P.StartInfo.CreateNoWindow = false;  //是否显示运行窗口
                P.StartInfo.FileName = strFilePath; //调用的程序名
                P.StartInfo.UserName = "administrator"; //指定用户名
              //  P.StartInfo.Password = password; //指定明码，必须是安全字符串
                P.StartInfo.Domain = "administrators"; //指定用户名所在域，即所在用户组
               // P.StartInfo.Arguments = "/c" + "shutdown -f -s"; //调用程序执行的命令
                P.Start();
            }
            catch
            {
                
            }
        }

        #endregion

        #region 自定义控件UI事件
        /// <summary>
        /// 窗口加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoccameraOcx_Load(object sender, EventArgs e)
        {
            ReadIniFile readIni = new ReadIniFile(strPath + "\\sdnsystem.ini");
            sdn_dual = readIni.ReadValue("dualcamera", "dual"); //是否具有双目活体检测功能
            sdn_verify = readIni.ReadValue("dualcamera", "verify"); //是否具有人证比对功能
            sdn_readCardType= readIni.ReadValue("dualcamera", "type"); //获取身份证方式

            //修改控件大小

        }

        /// <summary>
        /// 再次比对
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAgain_Click(object sender, EventArgs e)
        {
            btnAgain.Visible = false;//比对按钮不可见 点击之后不可见 防止多次点击
            bStopPlay();
            bStartPlay2(0);
        }

        #endregion

    }
}
