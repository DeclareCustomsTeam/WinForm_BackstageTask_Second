using Common;
using iTextSharp.text;
using iTextSharp.text.pdf;
using MessagingToolkit.Barcode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using O2S.Components.PDFRender4NET;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace BackstageTask_Second
{
    public partial class BackstageTask : Form
    {
        bool working = false;
        bool working2 = false;
        bool working3 = false;
        bool working4 = false;
        bool working5 = false;
        bool working6 = false;
        bool working7 = false;

        string barcode = string.Empty;
        string sql = string.Empty;
        string guid = string.Empty;
        string direc_pdf = ConfigurationManager.AppSettings["filedir"];//文件服务器存放文件的一级目录
        string direc_img = ConfigurationManager.AppSettings["ImagePath"];//pdf转图片时图片文件的存放路径
        string compal_ftp_username = ConfigurationManager.AppSettings["compalftpusername"];
        string compal_ftp_psd = ConfigurationManager.AppSettings["compalftppassword"];
        System.Uri compal_ftp_uri = new Uri("ftp://" + ConfigurationManager.AppSettings["compalftpip"] + ":21");
        FtpHelper ftp = null;
        IDatabase db = SeRedis.redis.GetDatabase();

        FtpHelper ftp_mov = null;
        string compal_ftp_username_mov = ConfigurationManager.AppSettings["compalftpusername_mov"];
        string compal_ftp_psd_mov = ConfigurationManager.AppSettings["compalftppassword_mov"];
        System.Uri compal_ftp_uri_mov = new Uri("ftp://" + ConfigurationManager.AppSettings["compalftpip_mov"] + ":21");
        public BackstageTask()
        {
            InitializeComponent();
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!working)
            {
                working = true;
                try
                {
                    sql = @"select t.attachmentid,t.ID,a.filename,a.filesuffix from pdfshrinklog t   
                          left join List_Attachment a on t.attachmentid=a.id  where t.iscompress=0 order by ID desc";
                    DataTable dt = DBMgr.GetDataTable(sql);
                    if (dt.Rows.Count > 0)//如果一次性送多个文件进入压缩程序，会有错误提示,所以每次只送一条记录
                    {
                        DataRow dr = dt.Rows[0];
                        if (File.Exists(ConfigurationManager.AppSettings["filedir"] + dr["FILENAME"]))//先判断原始文件存在
                        {
                            //再对扩展名判断
                            if ((dr["FILESUFFIX"] + "").ToUpper() == "PDF" || (dr["FILESUFFIX"] + "").ToUpper() == ".PDF")
                            {
                                System.Diagnostics.Process.Start(@"D:\Apago\PDFShrink\PDFShrink.exe", @"D:\ftpserver\" + dr["FILENAME"]);
                                sql = "update pdfshrinklog set iscompress='1',shrinktime=sysdate WHERE ID='" + dr["ID"] + "'";
                                DBMgr.ExecuteNonQuery(sql);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.button1.Text = ex.Message;
                    working = false;
                }
            }
            working = false;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            this.timer1.Enabled = true;
            this.button1.Text = "运行中";
            this.button1.Enabled = false;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            ftp = new FtpHelper(compal_ftp_uri, compal_ftp_username, compal_ftp_psd);
            ftp_mov = new FtpHelper(compal_ftp_uri_mov, compal_ftp_username_mov, compal_ftp_psd_mov);
        }
        public enum Definition
        {
            One = 1, Two = 2, Three = 3, Four = 4, Five = 5, Six = 6, Seven = 7, Eight = 8, Nine = 9, Ten = 10
        }
        private void ConvertPDF2Image(string pdfInputPath, string imageOutputPath, string imageName, int startPageNum, int endPageNum, ImageFormat imageFormat, Definition definition)
        {
            PDFFile pdfFile = PDFFile.Open(pdfInputPath);
            if (!Directory.Exists(imageOutputPath))
            {
                Directory.CreateDirectory(imageOutputPath);
            }
            for (int i = startPageNum; i <= endPageNum; i++)
            {
                Bitmap pageImage = pdfFile.GetPageImage(i - 1, 56 * (int)definition);
                pageImage.Save(imageOutputPath + imageName + "." + imageFormat.ToString(), imageFormat);
                pageImage.Dispose();
            }
            pdfFile.Dispose();
        }
        private void timer2_Tick(object sender, EventArgs e)
        {
            if (!working2)
            {
                working2 = true;
                if (db.KeyExists("recognizetask"))
                {
                    string json = db.ListLeftPop("recognizetask");
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            JObject jo = (JObject)JsonConvert.DeserializeObject(json);
                            //只有PDF文件才会进行条形码识别
                            sql = @"select t.* from list_attachment t where t.ordercode='" + jo.Value<string>("ordercode") + "' and upper(t.filesuffix)='PDF'";
                            DataTable dt = DBMgr.GetDataTable(sql);
                            if (dt.Rows.Count > 0)
                            {
                                guid = Guid.NewGuid().ToString();
                                ConvertPDF2Image(direc_pdf + dt.Rows[0]["FILENAME"], direc_img, guid, 1, 1, ImageFormat.Jpeg, Definition.Ten);
                                BarcodeDecoder barcodeDecoder = new BarcodeDecoder();
                                if (File.Exists(direc_img + guid + ".Jpeg"))
                                {
                                    System.Drawing.Bitmap image = new System.Drawing.Bitmap(direc_img + guid + ".Jpeg");
                                    Dictionary<DecodeOptions, object> decodingOptions = new Dictionary<DecodeOptions, object>();
                                    List<BarcodeFormat> possibleFormats = new List<BarcodeFormat>(10);
                                    //possibleFormats.Add(BarcodeFormat.DataMatrix);
                                    //possibleFormats.Add(BarcodeFormat.QRCode);
                                    //possibleFormats.Add(BarcodeFormat.PDF417);
                                    //possibleFormats.Add(BarcodeFormat.Aztec);
                                    //possibleFormats.Add(BarcodeFormat.UPCE);
                                    //possibleFormats.Add(BarcodeFormat.UPCA);
                                    possibleFormats.Add(BarcodeFormat.Code128);
                                    //possibleFormats.Add(BarcodeFormat.Code39);
                                    //possibleFormats.Add(BarcodeFormat.ITF14);
                                    //possibleFormats.Add(BarcodeFormat.EAN8);
                                    possibleFormats.Add(BarcodeFormat.EAN13);
                                    //possibleFormats.Add(BarcodeFormat.RSS14);
                                    //possibleFormats.Add(BarcodeFormat.RSSExpanded);
                                    //possibleFormats.Add(BarcodeFormat.Codabar);
                                    //possibleFormats.Add(BarcodeFormat.MaxiCode);
                                    decodingOptions.Add(DecodeOptions.TryHarder, true);
                                    decodingOptions.Add(DecodeOptions.PossibleFormats, possibleFormats);
                                    Result decodedResult = barcodeDecoder.Decode(image, decodingOptions);
                                    //while (decodedResult == null)
                                    //{
                                    //    System.Threading.Thread.Sleep(500);
                                    //}
                                    if (decodedResult != null)//有些PDF文件并无条形码
                                    {
                                        barcode = decodedResult.Text;
                                        barconvert();//编码转换 
                                        sql = "update list_order set cusno='" + barcode + "' where code='" + jo.Value<string>("ordercode") + "'";
                                        DBMgr.ExecuteNonQuery(sql);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            this.button2.Text = ex.Message;
                            this.button2.Text = json + "识别条码失败!";
                            //db.ListRightPush("recognizetask", json);
                            working2 = false;
                        }
                    }
                }
                working2 = false;
            }
        }
        private string barconvert()
        {
            string json_convert = @"{'01':'AIKSQW','02':'AIKSQN','03':'SIKS','04':'SEKSA','05':'SEKSB','06':'SEKSC','07':'SEKSD',
                                     '08':'SEKSE','09':'SEKSF','10':'SEKSG','11':'SEKSH','12':'SEKSI','13':'SEKSJ','14':'SEKSK',
                                     '15':'SEKSL','16':'SEKSM','17':'SEKSN','18':'SEKSO','19':'SEKSP','20':'SEKSQ','21':'SEKSR',
                                     '22':'SEKSS','23':'SEKST','24':'SEKSU','25':'SEKSV','26':'SEKSW','27':'SEKSX','28':'SEKSY',
                                     '29':'SEKSZ','30':'ILY','31':'ELY','35':'AEKS','45':'DJRIKS','46':'DJREKS','47':'DJCIKS',
                                     '48':'DJCEKS','49':'JGIKS','50':'JGEKS','51':'GJIKS','52':'GJEKS'}";
            JObject jo_convert = (JObject)JsonConvert.DeserializeObject(json_convert);
            string prefix = barcode.Substring(0, 2);
            if (!string.IsNullOrEmpty(jo_convert.Value<string>(prefix)))
            {
                barcode = jo_convert.Value<string>(prefix) + barcode.Substring(2);
            }
            return barcode;
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            //读取仁宝FTP服务器目录下所有的文件
            if (!working3)
            {
                working3 = true;
                try
                {
                    string destination = DateTime.Now.ToString("yyyy-MM-dd");
                    List<FileStruct> fis = ftp.GetFileAndDirectoryList(@"\");
                    foreach (FileStruct fs in fis)
                    {
                        int seconds = Convert.ToInt32((DateTime.Now - fs.UpdateTime.Value).TotalSeconds);
                        #region 处理文件开始
                        if (!fs.IsDirectory && fs.Size > 0 && seconds > 10)//有时候文件还在生成中，故加上时间范围限制
                        {
                            //提取合同协议号 如果无_，则直接将文件主名称作为合同协议号,如果有,则截取
                            int start = fs.Name.IndexOf("_");
                            string contractno = string.Empty;
                            if (start >= 0)
                            {
                                contractno = fs.Name.Substring(0, start);
                            }
                            else
                            {
                                start = fs.Name.IndexOf("-");//有些文件比较特殊是中杠
                                if (start >= 0)
                                {
                                    contractno = fs.Name.Substring(0, start);
                                }
                                else
                                {
                                    start = fs.Name.IndexOf(".");
                                    contractno = fs.Name.Substring(0, start);
                                }
                            }
                            bool content = update_entorder(fs, destination, contractno);
                            //如果数据库信息插入或者更新成功
                            if (content)
                            {
                                if (!Directory.Exists(direc_pdf + destination))
                                {
                                    Directory.CreateDirectory(direc_pdf + destination);
                                }
                                bool result = false;
                                if (fs.Name.IndexOf(".txt") > 0 || fs.Name.IndexOf(".TXT") > 0)
                                {
                                    string[] split = fs.Name.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                                    result = ftp.DownloadFile(@"\" + fs.Name, direc_pdf + destination + @"\" + split[0] + "_0." + split[1]);
                                    if (result) //TXT文件在下载成功的情况下
                                    {
                                        try
                                        {
                                            StreamReader sr = new StreamReader(direc_pdf + destination + @"\" + split[0] + "_0." + split[1], Encoding.GetEncoding("BIG5"));
                                            String line;
                                            FileStream fs2 = new FileStream(direc_pdf + destination + @"\" + fs.Name, FileMode.Create);
                                            while ((line = sr.ReadLine()) != null)
                                            {
                                                byte[] dst = Encoding.UTF8.GetBytes(line);
                                                fs2.Write(dst, 0, dst.Length);
                                                fs2.WriteByte(13);
                                                fs2.WriteByte(10);
                                            }
                                            fs2.Flush();  //清空缓冲区、关闭流
                                            fs2.Close();
                                        }
                                        catch (Exception ex)
                                        {
                                            this.button3.Text = ex.Message;
                                            working3 = false;
                                            break;//add by panhuaguo 20170118
                                        }
                                    }
                                    else
                                    {
                                        working3 = false;//如果txt文件下载失败
                                        break;
                                    }
                                }
                                else
                                {
                                    result = ftp.DownloadFile(@"\" + fs.Name, direc_pdf + destination + @"\" + fs.Name);
                                }
                                if (result)//下载成功的情况下
                                {
                                    ftp.MoveFile(@"\" + fs.Name, @"\backup\" + fs.Name);
                                }
                                else
                                {
                                    working3 = false;//如果f文件下载失败
                                    break;
                                }
                            }
                            else
                            {
                                working3 = false;//数据库写入失败
                                break;
                            }
                        }
                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    this.button3.Text = ex.Message;
                }
                finally
                {
                    working3 = false;
                }
            }
        }
        private bool update_entorder(FileStruct fs, string directory, string contractno)
        {
            bool content = false;
            #region
            try
            {
                string enterprisecode = string.Empty;
                string enterprisename = string.Empty;
                string prefix = fs.Name.Substring(0, 3);
                string entid = string.Empty;
                if (prefix == "E1P" || prefix == "E1B" || prefix == "IMP" || prefix == "IMB")
                {
                    enterprisecode = "3223640003";//海关10位编码  空运出口
                    enterprisename = "仁宝电子科技(昆山)有限公司";
                }
                if (prefix == "E1W" || prefix == "E2W" || prefix == "E1D" || prefix == "E2D" || prefix == "E7D" || prefix == "IMW" || prefix == "IMD" || prefix == "LMW" || prefix == "LMD" || prefix == "IAD" || prefix == "IEW" || prefix == "IED" || prefix == "E7W" || prefix == "LDD" || prefix == "LGW" || prefix == "LGD" || prefix == "LDW")
                {
                    enterprisecode = "3223640047";
                    enterprisename = "仁宝信息技术(昆山)有限公司";
                }
                if (prefix == "E1C" || prefix == "E1Q" || prefix == "E1O" || prefix == "IMQ" || prefix == "IMC" || prefix == "E2Q" || prefix == "E2C" || prefix == "LGC")
                {
                    enterprisecode = "3223640038";
                    enterprisename = "仁宝资讯工业(昆山)有限公司";
                }
                if (prefix == "IVS" || prefix == "EAS")
                {
                    enterprisecode = "3223660037";
                    enterprisename = "昆山柏泰电子技术服务有限公司";
                }
                //code是企业编号 仁宝格式 E1Q1603927_sheet.txt 
                int start = fs.Name.LastIndexOf("_");
                int end = fs.Name.LastIndexOf(".");
                string suffix = fs.Name.Substring(end + 1, 3).ToUpper();//文件扩展名
                string filetype = fs.Name.Substring(start + 1, end - start - 1).ToUpper();
                int filetypeid = 0;
                switch (filetype)
                {
                    case "CONTRACT":
                        filetypeid = 50;
                        break;
                    case "INVOICE":
                        filetypeid = 51;
                        break;
                    case "PACKING":
                        filetypeid = 52;
                        break;
                    case "SHEET":
                        filetypeid = 44;
                        break;
                    default:
                        filetypeid = 50;
                        break;
                }
                sql = "select * from ent_order where code='" + contractno + "'";
                DataTable dt_ent = DBMgr.GetDataTable(sql);
                if (dt_ent.Rows.Count == 0)
                {
                    sql = "select ENT_ORDER_ID.Nextval from dual";
                    entid = DBMgr.GetDataTable(sql).Rows[0][0] + "";
                    sql = @"insert into ent_order(ID,CODE,CREATETIME,SUBMITTIME,UNITCODE,ENTERPRISECODE,ENTERPRISENAME,FILEDECLAREUNITCODE,FILEDECLAREUNITNAME,
                            FILERECEVIEUNITCODE,FILERECEVIEUNITNAME,TEMPLATENAME,CUSTOMDISTRICTCODE,CUSTOMDISTRICTNAME) VALUES
                            ('{3}','{0}',sysdate,sysdate,(select fun_AutoQYBH(sysdate) from dual),'{1}','{2}','{4}','{5}','{6}','{7}','COMPAL01','2369','昆山综保')";
                    sql = string.Format(sql, contractno, enterprisecode, enterprisename, entid, "3223980002", "江苏飞力达国际物流股份有限公司", "3223980002", "江苏飞力达国际物流股份有限公司");
                    DBMgr.ExecuteNonQuery(sql);
                }
                else
                {
                    entid = dt_ent.Rows[0]["ID"] + "";
                }
                //写入随附文件表 
                sql = @"select * from list_attachment where originalname='" + fs.Name + "' and entid='" + entid + "'";
                DataTable dt_att = DBMgr.GetDataTable(sql);//因为客户有可能会重复传文件,此是表记录不需要变化，替换文件即可
                if (dt_att.Rows.Count > 0)
                {
                    sql = "delete from list_attachment where id='" + dt_att.Rows[0]["ID"] + "'";
                    DBMgr.ExecuteNonQuery(sql);
                }
                //dt_att = DBMgr.GetDataTable("select LIST_ATTACHMENT_ID.Nextval ATTACHMENTID from dual");
                sql = @"insert into list_attachment(ID,FILENAME,ORIGINALNAME,UPLOADTIME,FILETYPE,SIZES,ENTID,FILESUFFIX,UPLOADUSERID,CUSTOMERCODE,isupload) values(
                   LIST_ATTACHMENT_ID.Nextval,'{0}','{1}',sysdate,'{2}','{3}','{4}','{5}','404','{6}','1')";
                sql = string.Format(sql, "/" + directory + "/" + fs.Name, fs.Name, filetypeid, fs.Size, entid, suffix, enterprisecode);
                int result = DBMgr.ExecuteNonQuery(sql);
                if (result > 0 && fs.Name.IndexOf(".txt") > 0)
                {
                    db.ListRightPush("compal_sheet_topdf_queen", "{ENTID:'" + entid + "',FILENAME:" + "'/" + directory + "/" + fs.Name + "'}");//保存随附文件ID到队列
                }
                content = true;
            }
            #endregion
            catch (Exception ex)
            {
                this.button3.Text = "database_" + ex.Message;
                content = false;
            }
            return content;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.timer2.Enabled = true;
            this.button2.Text = "运行中...";
            this.button2.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            this.timer3.Enabled = true;
            this.button3.Text = "运行中...";
            this.button3.Enabled = false;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            this.timer4.Enabled = true;
            this.button4.Text = "运行中...";
            this.button4.Enabled = false;
        }

        private void timer4_Tick(object sender, EventArgs e)
        {
            if (!working4)
            {
                working4 = true;
                string json = string.Empty;
                try
                {
                    json = db.ListLeftPop("compal_sheet_topdf_queen");
                    if (!string.IsNullOrEmpty(json))
                    {
                        JObject jo = (JObject)JsonConvert.DeserializeObject(json);//转JSON对象
                        if (File.Exists(ConfigurationManager.AppSettings["filedir"] + jo.Value<string>("FILENAME")))
                        {
                            int index = jo.Value<string>("FILENAME").LastIndexOf(".");
                            string preffix = jo.Value<string>("FILENAME").Substring(0, index + 1);
                            Document doc = new Document(PageSize.A4.Rotate());
                            PdfWriter.GetInstance(doc, new FileStream(ConfigurationManager.AppSettings["filedir"] + preffix + "pdf", FileMode.Create));
                            doc.Open();
                            //中文字型問題REF http://renjin.blogspot.com/2009/01/using-chinese-fonts-in-itextsharp.html                       
                            string fontPath = Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\..\Fonts\kaiu.ttf";
                            //橫式中文
                            BaseFont bfChinese = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.NOT_EMBEDDED);
                            iTextSharp.text.Font fontChinese = new iTextSharp.text.Font(bfChinese, 8f);
                            StreamReader sr = new StreamReader(ConfigurationManager.AppSettings["filedir"] + jo.Value<string>("FILENAME"));
                            string line = null;
                            while ((line = sr.ReadLine()) != null)
                            {
                                doc.Add(new Paragraph(line, fontChinese));
                            }
                            doc.Close();
                            sql = "update list_attachment set FILENAME='" + preffix + "pdf" + "' where  FILENAME='" + jo.Value<string>("FILENAME") + "' and ENTID='" + jo.Value<string>("ENTID") + "'";
                            DBMgr.ExecuteNonQuery(sql);
                        }
                    }
                    working4 = false;
                }
                catch (Exception ex)
                {
                    db.ListRightPush("compal_sheet_topdf_queen", json);
                    this.button4.Text = ex.Message;
                    working4 = false;
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            this.timer5.Enabled = true;
            this.button5.Text = "运行中...";
            this.button5.Enabled = false;
        }

        private void timer5_Tick(object sender, EventArgs e)
        {
            if (!working5)
            {
                working5 = true;
                redistotable();
                working5 = false;
            }
        }
        private void redistotable()
        {
            if (db.KeyExists("declareall"))
            {
                string json = db.ListLeftPop("declareall");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        JObject jo = (JObject)JsonConvert.DeserializeObject(json);
                        sql = @"insert into redis_declareall (ID,DECLARATIONCODE,TRADECODE,TRANSNAME,GOODSNUM,GOODSGW,SHEETNUM,COMMODITYNUM,
                              CUSTOMSSTATUS,MODIFYFLAG,PREDECLCODE,CUSNO,OLDDECLARATIONCODE,ISDEL,DIVIDEREDISKEY) values (REDIS_DECLAREALL_ID.Nextval,
                              '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}','{13}')";
                        sql = string.Format(sql, jo.Value<string>("DECLARATIONCODE"), jo.Value<string>("TRADECODE"), jo.Value<string>("TRANSNAME"),
                            jo.Value<string>("GOODSNUM"), jo.Value<string>("GOODSGW"), jo.Value<string>("SHEETNUM"), jo.Value<string>("COMMODITYNUM"),
                            jo.Value<string>("CUSTOMSSTATUS"), jo.Value<string>("MODIFYFLAG"), jo.Value<string>("PREDECLCODE"), jo.Value<string>("CUSNO"),
                            jo.Value<string>("OLDDECLARATIONCODE"), jo.Value<string>("ISDEL"), jo.Value<string>("DIVIDEREDISKEY"));
                        DBMgr.ExecuteNonQuery(sql);
                    }
                    catch (Exception ex)
                    {
                        this.button5.Text = ex.Message;
                        db.ListRightPush("declareall", json);
                    }
                }
            }
            if (db.KeyExists("inspectionall"))
            {
                string json = db.ListLeftPop("inspectionall");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        JObject jo = (JObject)JsonConvert.DeserializeObject(json);
                        sql = @"insert into REDIS_INSPECTIONALL (ID,APPROVALCODE,INSPECTIONCODE,TRADEWAY,CLEARANCECODE,SHEETNUM,
                            COMMODITYNUM,INSPSTATUS,MODIFYFLAG,PREINSPCODE,CUSNO, OLDINSPECTIONCODE ,ISDEL,ISNEEDCLEARANCE,
                            LAWFLAG,DIVIDEREDISKEY) values (REDIS_INSPECTIONALL_ID.Nextval,
                            '{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}','{13}','{14}')";
                        sql = string.Format(sql, jo.Value<string>("APPROVALCODE"), jo.Value<string>("INSPECTIONCODE"), jo.Value<string>("TRADEWAY"),
                            jo.Value<string>("CLEARANCECODE"), jo.Value<string>("SHEETNUM"), jo.Value<string>("COMMODITYNUM"), jo.Value<string>("INSPSTATUS"),
                            jo.Value<string>("MODIFYFLAG"), jo.Value<string>("PREINSPCODE"), jo.Value<string>("CUSNO"), jo.Value<string>("OLDINSPECTIONCODE"),
                            jo.Value<string>("ISDEL"), jo.Value<string>("ISNEEDCLEARANCE"), jo.Value<string>("LAWFLAG"), jo.Value<string>("DIVIDEREDISKEY"));
                        DBMgr.ExecuteNonQuery(sql);
                    }
                    catch (Exception ex)
                    {
                        this.button5.Text = ex.Message;
                        db.ListRightPush("inspectionall", json);
                    }
                }
            }
            if (db.KeyExists("statuslogall"))
            {
                string json = db.ListLeftPop("statuslogall");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        JObject jo = (JObject)JsonConvert.DeserializeObject(json);
                        sql = @"insert into redis_statuslogall (ID,TYPE,CUSNO,STATUSCODE,STATUSVALUE,DIVIDEREDISKEY) values (REDIS_STATUSLOGALL_ID.Nextval,
                            '{0}','{1}','{2}','{3}','{4}')";
                        sql = string.Format(sql, jo.Value<string>("TYPE"), jo.Value<string>("CUSNO"), jo.Value<string>("STATUSCODE"),
                            jo.Value<string>("STATUSVALUE"), jo.Value<string>("DIVIDEREDISKEY"));
                        DBMgr.ExecuteNonQuery(sql);
                    }
                    catch (Exception ex)
                    {
                        this.button5.Text = ex.Message;
                        db.ListRightPush("statuslogall", json);
                    }
                }
            }
        }

        #region 这两个只是暂时使用的功能
        private void button6_Click(object sender, EventArgs e)
        {
            this.timer6.Enabled = true;
            this.button6.Text = "运行中";
            this.button6.Enabled = false;
        }
        private void timer6_Tick(object sender, EventArgs e)
        {
            if (!working6)
            {
                working6 = true;
                try
                {
                    PdfReader pdfReader;
                    string sql = @"select * from FILEMANAGE where businessno is not null and filetype='CustomsFile' and valid IS NULL and filename is null 
                            and createtime >= to_date('2016-10-1 0:00:00','yyyy-mm-dd hh24:mi:ss') and createtime < to_date('2016-11-10 0:00:00','yyyy-mm-dd hh24:mi:ss') 
                            and ROWNUM<=5";

                    DataTable dt_filemanage = DBMgrMov.GetDataTable(sql);
                    string ordercode = string.Empty;
                    //int attachmentid=2;and createtime > to_date('2015-12-31 23:59:59','yyyy-mm-dd hh24:mi:ss') and ROWNUM<=5";
                    foreach (DataRow dr in dt_filemanage.Rows)
                    {
                        string fileid = string.Empty;
                        string filemanageid = string.Empty;
                        ordercode = dr["businessno"].ToString();
                        DataTable dt_list = DBMgr.GetDataTable("select * from list_attachment where ORDERCODE='" + ordercode + "' and filetype=44");
                        if (dt_list.Rows.Count == 0)
                        {
                            fileid = dr["fileid"].ToString();
                            filemanageid = dr["id"].ToString();
                            DataTable dt_fileitem = DBMgrMov.GetDataTable("select * from fileitem where id='" + fileid + "'");
                            //DataTable dt_filemanagedetail = DBMgrMov.GetDataTable("select * from filemanagedetail where filemanageid='" + filemanageid + "'");
                            if (dt_fileitem.Rows.Count != 0)
                            {
                                string fileitemPath = dt_fileitem.Rows[0]["path"].ToString().Trim();
                                string fileitemName = dt_fileitem.Rows[0]["name"].ToString().Trim();
                                string item_id = dt_fileitem.Rows[0]["id"].ToString().Trim();

                                //if (!Directory.Exists(direc_pdf + @"\item\"+fileitemPath))
                                //{
                                //    Directory.CreateDirectory(direc_pdf + @"\item\"+fileitemPath); 
                                //}
                                //bool success = ftp.DownloadFile(fileitemPath + fileitemName, direc_pdf + @"\item\" + fileitemPath + fileitemName);
                                string filename = @"/item/" + fileitemPath + "/" + fileitemName;
                                string originalname = fileitemName;
                                int filetype = 44;
                                int sizes = Convert.ToInt32(dt_fileitem.Rows[0]["filesize"].ToString());
                                int isupload = 1;
                                string filesuffix = dt_fileitem.Rows[0]["extname"].ToString();
                                string filetypename = "订单文件";
                                int splitstatus = 1;
                                //if (dt_filemanagedetail.Rows.Count != 0)
                                //{
                                //    splitstatus = 1;
                                //}
                                string url = "http://172.20.70.98:7003/Document/" + fileitemPath + "/" + item_id + "_" + fileitemName;
                                int filepages = 0;

                                System.Uri uri = new Uri(url);
                                //pdfReader = new PdfReader(@"d:\ftpserver\" + filename);
                                pdfReader = new PdfReader(uri);
                                filepages = pdfReader.NumberOfPages;
                                pdfReader.Close();


                                //更新数据附件数据表
                                string sqlupdate = "insert into list_attachment (filename,originalname,filetype,sizes,ordercode,filesuffix,filetypename,splitstatus,isupload,filepages,uploadtime,ordercount) "
                                                 + "values  ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}',to_date('" + dr["createtime"].ToString() + "','yyyy-MM-dd HH24:mi:ss'),0)";
                                sqlupdate = string.Format(sqlupdate, filename, originalname, filetype, sizes, ordercode, filesuffix, filetypename, splitstatus, isupload, filepages);
                                int updateSuccess = DBMgr.ExecuteNonQuery(sqlupdate);

                                //获取attachmentid
                                // DataTable dt_fileitem_new = DBMgrMov.GetDataTable("select id from list_attachment where ordercode='" + ordercode + "'");
                                //if (dt_fileitem_new.Rows.Count > 1)
                                //{
                                //    attachmentid = Convert.ToInt32(dt_fileitem_new.Rows[0]["id"].ToString());
                                //}

                            }
                        }
                        //if (dt_filemanagedetail.Rows.Count!=0)
                        //{

                        //    foreach (DataRow dr_filemanagedetail in dt_filemanagedetail.Rows)
                        //    {
                        //     string filename=dr_filemanagedetail["filename"].ToString().Replace(@"D:\RW\Files\AppFiles\Portal\","");
                        //     string path=filename.Substring(0,filename.IndexOf(@"\")+1);
                        //     //if (!Directory.Exists(direc_pdf + @"\detail\" + path))
                        //     //   {
                        //     //       Directory.CreateDirectory(direc_pdf + @"\detail\" + path);   
                        //     //   }
                        //     //bool success=ftp.DownloadFile(filename, direc_pdf + @"\detail\" + filename);
                        //         string sourcefilename = @"\detail\" + filename;
                        //         string filename_a=filename.Substring(filename.IndexOf(@"\") + 1, filename.Length);

                        //         int filetypeid=0;
                        //         string subtypename=dr_filemanagedetail["subtypename"].ToString();
                        //         switch (subtypename)
                        //        {
                        //            case "箱单":
                        //            filetypeid=52;
                        //            break;
                        //            case "其他1":
                        //            filetypeid=54;
                        //            break;
                        //            case "货物清单":
                        //            filetypeid=101;
                        //            break;
                        //            case "提运单":
                        //            filetypeid=49;
                        //            break;
                        //            case "委托协议":
                        //            filetypeid = 53;
                        //            break;
                        //            case "发票":
                        //            filetypeid=51;
                        //            break;
                        //            case "合同":
                        //            filetypeid=50;
                        //            break;
                        //        }
                        //         string pages = dr_filemanagedetail["pages"].ToString();
                        //         string spliteusername = dr_filemanagedetail["spliteusername"].ToString();
                        //         string splitetime = dr_filemanagedetail["splitetime"].ToString();
                        //            //更新详细文件表
                        //         string sqlupdate = "insert into list_attachmentdetail (sourcefilename,filename,attachmentid,filetypeid,splitetime,ordercode,pages,spliteusername) "
                        //                          + "values  ('{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}')";
                        //         sqlupdate = string.Format(sqlupdate, sourcefilename, filename_a, attachmentid, filetypeid,splitetime,ordercode,pages,spliteusername);
                        //         DBMgrMov.ExecuteNonQuery(sqlupdate);

                        //    }   

                        //}
                        DBMgrMov.ExecuteNonQuery("update FILEMANAGE  set filename='Y' where businessno='" + ordercode + "' and filetype='CustomsFile' and valid IS NULL and filename is null");
                    }
                }
                catch (Exception ex)
                {

                    this.button6.Text = ex.Message;
                    working6 = false;
                }

                working6 = false;
            }

        }

        private void button7_Click(object sender, EventArgs e)
        {
            this.timer7.Enabled = true;
            this.button7.Text = "运行中";
            this.button7.Enabled = false;
        }
        private void timer7_Tick(object sender, EventArgs e)
        {
            PdfReader pdfReader;

            if (!working7)
            {
                working7 = true;
                try
                {
                    string sql = "select l.*,o.filepages as pages,o.code from list_attachment l left join list_order o on l.ORDERCODE=o.CODE WHERE l.splitstatus=1 and l.filepages is null and  ROWNUM <=100";
                    DataTable dt = DBMgr.GetDataTable(sql);
                    if (dt.Rows.Count != 0)
                    {
                        foreach (DataRow dr in dt.Rows)
                        {
                            string filename = dr["FILENAME"].ToString();
                            int id = Convert.ToInt32(dr["ID"].ToString());
                            pdfReader = new PdfReader(@"d:\ftpserver\" + filename);
                            int totalPages = pdfReader.NumberOfPages;
                            pdfReader.Close();

                            DBMgr.ExecuteNonQuery("update list_attachment set FILEPAGES=" + totalPages + " where id=" + id);
                            if (dr["pages"] == null && dr["code"] != null)
                            {
                                DBMgr.ExecuteNonQuery("update list_order set FILEPAGES=" + totalPages + " where CODE='" + dr["code"].ToString() + "'");
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    this.button7.Text = ex.Message;
                    working7 = false;


                }
                working7 = false;
            }

        }

    }
}
        #endregion