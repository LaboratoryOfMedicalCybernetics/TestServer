using System;
using System.Windows.Forms;
using System.Net;
using System.IO;
using NetManager;
using EEG;
using System.Threading;

namespace TestServer
{
    public partial class Form1 : Form
    {
        private Random m_rnd;

        public Form1()
        {
            InitializeComponent();

            m_NMClient = new NMClient(this);
            m_NMClient.OnDeleteClient += new EventHandler<EventClientArgs>(NMClient_OnDeleteClient);
            m_NMClient.OnError += new EventHandler<EventMsgArgs>(NMClient_OnError);
            m_NMClient.OnNewClient += new EventHandler<EventClientArgs>(NMClient_OnNewClient);
            m_NMClient.OnReseive += new EventHandler<EventClientMsgArgs>(NMClient_OnReseive);
            m_NMClient.OnStop += new EventHandler(NMClient_OnStop);

            m_rnd = new Random((int)DateTime.Now.Ticks);
        }

        void NMClient_OnStop(object sender, EventArgs e)
        {
            btnConnect.Text = "Подключить";
            btnSendFile.Enabled = false;
            chClients.Enabled = true;
            chClients.Items.Clear();
        }

        void NMClient_OnReseive(object sender, EventClientMsgArgs e)
        {
            Wait = BitConverter.ToInt32(e.Msg, 0) != 4;
        }

        void NMClient_OnNewClient(object sender, EventClientArgs e)
        {
            chClients.Items.Add(new ClientAddress(e.ClientId, e.Name));
        }

        void NMClient_OnError(object sender, EventMsgArgs e)
        {
            MessageBox.Show(e.Msg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void NMClient_OnDeleteClient(object sender, EventClientArgs e)
        {
            ClientAddress Cl = new ClientAddress(e.ClientId, e.Name);
            int I = chClients.Items.Count - 1;
            while ((I >= 0) && (Cl.ToString() != chClients.Items[I].ToString()))
                I--;
            if (I >= 0)
                chClients.Items.RemoveAt(I);
        }

        private int Port
        {
            get
            {
                return Convert.ToInt32(tbPort.Text);
            }
            set
            {
                tbPort.Text = value.ToString();
            }
        }

        private NMClient m_NMClient;

        private delegate void SendData();

        /// <summary>
        /// Определяет ожидание пуска сигнала
        /// </summary>
        private bool m_Wait;

        private bool Wait
        {
            get
            {
                return m_Wait;
            }
            set
            {
                if (m_Wait != value)
                {
                    if (value)
                        statusStrip1.Invoke(new Action(delegate { statusStrip1.Items[0].Text = "Ожидание"; }));
                    else
                        statusStrip1.Invoke(new Action(delegate { statusStrip1.Items[0].Text = "Отправка данных"; }));
                    m_Wait = value;
                }
            }
        }

        private void Send_Data()
        {
            Frame data = new Frame();
            int[] addresses = new int[chClients.CheckedItems.Count];

            for (int j = 0; j < chClients.CheckedItems.Count; j++)
                addresses[j] = (chClients.CheckedItems[j] as ClientAddress).Id;

            StreamReader[] baseFiles = null;
            int currentIndex = 0;
            if (rbFile.Checked)
            {
                baseFiles = new StreamReader[dgBaseFiles.RowCount];
                for (int i = 0; i < dgBaseFiles.RowCount; i++)
                {
                    try
                    {
                        baseFiles[i] = new StreamReader(dgBaseFiles.Rows[i].Cells[1].Value.ToString());
                        if (baseFiles[i].EndOfStream)
                            throw new Exception("Файл не может быть пустым");
                    }
                    catch (Exception e)
                    {
                        baseFiles[i] = null;
                        dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[i].Cells[2].Value = "Ошибка '" + e.Message + "'"; }));
                    }
                }
                while (currentIndex < dgBaseFiles.RowCount && baseFiles[currentIndex] == null)
                    currentIndex++;
                if (currentIndex == dgBaseFiles.RowCount)
                {
                    MessageBox.Show("Ни один указанный файл не может использоваться в качестве фонового. Будет использован режим белого шума.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    rbFantom.Invoke(new Action(delegate { rbFantom.Checked = true; }));
                }
                else
                    dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = "Отправка"; }));
            }

            Action<Frame> getFrame = delegate (Frame frame)
            {
                if (rbFantom.Checked)
                {
                    for (int i = 0; i < frame.Data.Length; i++)
                        frame.Data[i] = (short)(m_rnd.Next(2 * (int)nAmplitude.Value) - nAmplitude.Value);
                }
                else
                {
                    int i = 0;
                    while (i < Frame.LengthData)
                    {
                        try
                        {
                            if (baseFiles[currentIndex].EndOfStream)
                            {
                                baseFiles[currentIndex].BaseStream.Position = 0;
                                currentIndex = (currentIndex + 1) % dgBaseFiles.RowCount;
                                dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = ""; }));
                                while (baseFiles[currentIndex] == null)
                                    currentIndex = (currentIndex + 1) % dgBaseFiles.RowCount;
                                dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = "Отправка"; }));
                            }
                            string[] strs = baseFiles[currentIndex].ReadLine().Split(new char[] { ' ', (char)9, ';' });
                            for (int j = 0; j < Math.Min(strs.Length, Frame.CountChannels); j++)
                                frame.Data[j * Frame.LengthData + i] = short.Parse(strs[j]);
                            i++;
                        }
                        catch (Exception e)
                        {
                            dgSignalFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = "Ошибка '" + e.Message + "'"; }));
                            baseFiles[currentIndex] = null;
                            int k = currentIndex;
                            dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = ""; }));
                            currentIndex = (currentIndex + 1) % dgBaseFiles.RowCount;
                            while (currentIndex != k && baseFiles[currentIndex] == null)
                                currentIndex = (currentIndex + 1) % dgBaseFiles.RowCount;
                            if (currentIndex == k)
                            {
                                MessageBox.Show("Ни один указанный файл не может использоваться в качестве фонового. Будет использован режим белого шума.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                rbFantom.Invoke(new Action(delegate { rbFantom.Checked = true; }));
                                i = Frame.LengthData;
                            }
                            else
                                dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = "Отправка"; }));
                        }
                    }
                }
            };

            while (m_NMClient.Running)
            {
                if (!Wait)
                {
                    if ((chClients.CheckedItems.Count > 0) && m_NMClient.Running)
                    {
                        string str;
                        for (int l = 0; l < dgSignalFiles.Rows.Count; l++)
                        {
                            try
                            {
                                StreamReader sr = new StreamReader(dgSignalFiles.Rows[l].Cells[1].Value.ToString());
                                dgSignalFiles.Invoke(new Action(delegate { dgSignalFiles.Rows[l].Cells[2].Value = "Отправка"; }));
                                string[] strs;
                                int i = 0;
                                while (!sr.EndOfStream)
                                {
                                    if (i == Frame.LengthData)
                                    {
                                        i = 0;
                                        m_NMClient.SendData(addresses, data.GetBytes());
                                        Thread.Sleep(4);
                                    }
                                    str = sr.ReadLine();
                                    strs = str.Split(new char[] { ' ', (char)9, ';' });
                                    if (i == 0)
                                        getFrame(data);
                                    for (int j = 0; j < Math.Min(strs.Length, Frame.CountChannels); j++)
                                        data.Data[j * Frame.LengthData + i] += short.Parse(strs[j]);
                                    i++;
                                }
                                dgSignalFiles.Invoke(new Action(delegate { dgSignalFiles.Rows[l].Cells[2].Value = ""; }));
                                sr.Close();
                            }
                            catch (Exception e)
                            {
                                    dgSignalFiles.Invoke(new Action(delegate { dgSignalFiles.Rows[l].Cells[2].Value = "Ошибка '" + e.Message + "'"; }));
                            }
                        }
                    }
                    Wait = true;
                }
                else
                {
                    getFrame(data);
                    m_NMClient.SendData(addresses, data.GetBytes());
                    Thread.Sleep(4);
                }
            }

            if (rbFile.Checked)
                dgBaseFiles.Invoke(new Action(delegate { dgBaseFiles.Rows[currentIndex].Cells[2].Value = ""; }));


            SetEnabled(btnSendFile, true);
            SetEnabled(chClients, true);
        }

        private delegate void delegate_SetEnabled(Control Control, bool Enabled);

        private void SetEnabled(Control Control, bool Enabled)
        {
            if (Control.InvokeRequired)
            {
                delegate_SetEnabled E = new delegate_SetEnabled(SetEnabled);
                Control.Invoke(E, new object[] {Control, Enabled});
            }
            else
                Control.Enabled = Enabled;
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (!m_NMClient.Running)
            {
                m_NMClient.IPServer = IPAddress.Parse(tbIP.Text);
                m_NMClient.Port = Int32.Parse(tbPort.Text);
                m_NMClient.Name = tbName.Text;
                m_NMClient.RunClient();
                btnConnect.Text = "Отключить";
                btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
            }
            else
                m_NMClient.StopClient();
        }

        private void btnSendFile_Click(object sender, EventArgs e)
        {
            btnSendFile.Enabled = false;
            chClients.Enabled = false;
            Wait = true;
            SendData SD = new SendData(Send_Data);
            SD.BeginInvoke(null, null);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_NMClient.Running)
                m_NMClient.StopClient();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            dgSignalFiles.Rows.Clear();
            btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
                foreach (string file in openFileDialog1.FileNames)
                    dgSignalFiles.Rows.Add(dgSignalFiles.RowCount + 1, file, "");
            btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
        }

        private void удалитьToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgSignalFiles.SelectedRows)
                dgSignalFiles.Rows.Remove(row);
            btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
                foreach (string file in openFileDialog1.FileNames)
                    dgBaseFiles.Rows.Add(dgBaseFiles.RowCount + 1, file, "");
            btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
        }

        private void удалитьToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in dgBaseFiles.SelectedRows)
                dgBaseFiles.Rows.Remove(row);
            btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
        }

        private void button3_Click(object sender, EventArgs e)
        {
            dgBaseFiles.Rows.Clear();
            btnSendFile.Enabled = dgSignalFiles.RowCount > 0 && (rbFantom.Checked || (rbFile.Checked && dgBaseFiles.RowCount > 0));
        }

        private void rbFantom_CheckedChanged(object sender, EventArgs e)
        {
            if (rbFantom.Checked)
                rbFile.Checked = false;
        }

        private void rbFile_CheckedChanged(object sender, EventArgs e)
        {
            if (rbFile.Checked)
                rbFantom.Checked = false;
        }
    }
}
