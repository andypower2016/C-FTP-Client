using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace FTPClient
{
    public partial class Form1 : Form
    {

        static class CommandDefine
        {
            public const string ctransferStart = "501";
            public const string ctransfering = "502";
            public const string ctransferEnd = "503";

            public const string stransferStart = "601";
            public const string stransfering = "602";
            public const string stransferEnd = "603";

            public const byte packetHead = 0x000;
            public const byte dataHead = 0x001;
        }

        TcpClient tcpClient;
        OpenFileDialog openFileDlg;   
        
        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        public Form1()
        {
            InitializeComponent();

            openFileDlg = new OpenFileDialog();
            openFileDlg.Multiselect = true;

            textBoxIP.Text = "219.87.85.162";//GetLocalIPAddress();
            textBoxPort.Text = "10025";
        }

        private void Connect_Click(object sender, EventArgs e)
        {
            try
            {
                tcpClient = new TcpClient(textBoxIP.Text, Convert.ToInt32(textBoxPort.Text));
                //textBoxPort.Text = "192.168.165.170";
                //tcpClient = new TcpClient(); 
                //IPAddress ipAddress = Dns.GetHostEntry(textBoxPort.Text).AddressList[0];
                //IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, 100);
                //tcpClient.Connect(ipEndPoint);
            }
            catch (SocketException socketException)
            {
                string message;
                message = String.Format("Connect Fail, errorCode={0}", socketException.ErrorCode);
                MessageBox.Show(message);
            } 
        }

        private void buttonUploadFile_Click(object sender, EventArgs e)
        {
            if (openFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                int maxBufferLen = 50000;
                // Read the files
                foreach (String file in openFileDlg.FileNames)
                {
                    labelCurrentFile.Text = "Current transfering file : " + file;        
                    Boolean completeFlag = false;        
                    NetworkStream networkStream = tcpClient.GetStream();
                    FileStream fstream = new FileStream(file, FileMode.Open);                
                    byte[] fileNameData = CreateDataPacket(Encoding.UTF8.GetBytes(CommandDefine.ctransferStart), Encoding.UTF8.GetBytes(Path.GetFileName(file)));  
                    networkStream.Write(fileNameData, 0, fileNameData.Length);
                    networkStream.Flush();
                  
                    while (true)
                    {
                        if (networkStream.ReadByte() == CommandDefine.packetHead)
                        {
                            byte[] command = new byte[3];
                            networkStream.Read(command, 0, command.Length);                         
                            byte[] recvData = ReadDataPacket(networkStream);
                            switch (System.Text.Encoding.UTF8.GetString(command))
                            {
                                case CommandDefine.ctransfering:
                                    long curFilePointer = long.Parse(Encoding.UTF8.GetString(recvData));    // receive file pointer from server
                                    if (curFilePointer != fstream.Length && curFilePointer < fstream.Length)
                                    {
                                        fstream.Seek(curFilePointer, SeekOrigin.Begin);
                                        int sendBufferLen = (int)( (fstream.Length - curFilePointer) < maxBufferLen ? (fstream.Length - curFilePointer) : maxBufferLen );
                                        byte[] sendBuffer = new byte[sendBufferLen];
                                        fstream.Read(sendBuffer, 0, sendBuffer.Length);
                                        byte[] sendData = CreateDataPacket(Encoding.UTF8.GetBytes(CommandDefine.ctransfering), sendBuffer);  // send file data to Server
                                        networkStream.Write(sendData, 0, sendData.Length);
                                        networkStream.Flush();
                                        UpdateProgressBar(curFilePointer, fstream.Length);
                                    }
                                    else 
                                    {
                                        byte[] sendData = CreateDataPacket(Encoding.UTF8.GetBytes(CommandDefine.ctransferEnd), Encoding.UTF8.GetBytes("Close"));    // file transfer complete, send Close message
                                        networkStream.Write(sendData, 0, sendData.Length);
                                        networkStream.Flush();
                                        UpdateProgressBar(curFilePointer, fstream.Length);
                                        fstream.Close();
                                        completeFlag = true;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (completeFlag == true)
                        {
                            networkStream.Flush();
                            labelCurrentFile.Text = @"";
                            break;
                        }
                    }
                }
            }
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {
            string downloadFolder = @"D:\Me\Code\C#\FTP Server Code\download folder";
            openFileDlg.InitialDirectory = @"\\" + textBoxIP.Text + @"\D$";
            if (openFileDlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                NetworkStream networkStream = tcpClient.GetStream();
                FileStream fstream = null;

                foreach (String filePath in openFileDlg.FileNames)
                {
                    Boolean completeFlag = false;
                    long curfilePointer = 0;
                    string fileName = Path.GetFileName(filePath);
                    string downloadFilePath = downloadFolder + '\\' + fileName;
                    long fileLength = new System.IO.FileInfo(filePath).Length;
                    fstream = new FileStream(downloadFilePath, FileMode.Create);
                    byte[] filePathData = CreateDataPacket(Encoding.UTF8.GetBytes(CommandDefine.stransferStart), Encoding.UTF8.GetBytes(filePath));   // Send filePath to Server
                    networkStream.Write(filePathData, 0, filePathData.Length);
                    networkStream.Flush();
                    while (true)
                    {
                        if (networkStream.ReadByte() == CommandDefine.packetHead)
                        {
                            byte[] command = new byte[3];
                            networkStream.Read(command, 0, command.Length);
                            byte[] receivedData = ReadDataPacket(networkStream);
                            switch (System.Text.Encoding.UTF8.GetString(command))
                            {
                                case CommandDefine.stransfering:
                                    {
                                        fstream.Seek(curfilePointer, SeekOrigin.Begin);
                                        fstream.Write(receivedData, 0, receivedData.Length);
                                        curfilePointer = fstream.Position;
                                        byte[] sendData = CreateDataPacket(System.Text.Encoding.UTF8.GetBytes(CommandDefine.stransfering), System.Text.Encoding.UTF8.GetBytes(System.Convert.ToString(curfilePointer)));
                                        networkStream.Write(sendData, 0, sendData.Length);
                                        networkStream.Flush();
                                        UpdateProgressBarDownload(curfilePointer, fileLength);
                                    }
                                    break;
                                case CommandDefine.stransferEnd:
                                    {
                                        UpdateProgressBarDownload(curfilePointer, fileLength);
                                        curfilePointer = 0;
                                        networkStream.Flush();
                                        fstream.Close();
                                        completeFlag = true;
                                    }
                                    break;

                                default:
                                    break;
                            }
                            if (completeFlag)
                                break;
                        }
                    }
                }
            }
        }

        public void UpdateProgressBar(long progress, long total)
        {
            progressBar.Value = (int)Math.Ceiling((double)progress * 100 / (double)total);
            if (progressBar.Value == 100)
            {
                progressBar.Value = 0;
            }
        }

        public void UpdateProgressBarDownload(long progress, long total)
        {
            progressBarDownload.Value = (int)Math.Ceiling((double)progress * 100 / (double)total);
            if (progressBarDownload.Value == 100)
            {
                progressBarDownload.Value = 0;
            }
        }

        
        private byte[] ReadDataPacket(NetworkStream ns)
        {
            byte[] dataPacket = null;
            int length = 0;
            String strDataLength = "";
            while ((length = ns.ReadByte()) != CommandDefine.dataHead) // read data length
            {
                strDataLength += (char)length;
            }
            int dataLength = System.Convert.ToInt32(strDataLength);
            dataPacket = new byte[dataLength];

            int byteOffset = 0;
            while (byteOffset < dataLength)
            {
                byteOffset += ns.Read(dataPacket, byteOffset, dataLength - byteOffset);
            }
            return dataPacket;
        }

        private byte[] CreateDataPacket(byte[] cmd, byte[] data)
        {
            byte[] pacHead = new byte[1];
            pacHead[0] = CommandDefine.packetHead;
            byte[] datHead = new byte[1];
            datHead[0] = CommandDefine.dataHead;
            byte[] datalength = System.Text.Encoding.UTF8.GetBytes(System.Convert.ToString(data.Length));
            MemoryStream stream = new MemoryStream();
            stream.Write(pacHead, 0, pacHead.Length);
            stream.Write(cmd, 0, cmd.Length);
            stream.Write(datalength, 0, datalength.Length);
            stream.Write(datHead, 0, datHead.Length);
            stream.Write(data, 0, data.Length);
            return stream.ToArray();
        }

        
    }
}
