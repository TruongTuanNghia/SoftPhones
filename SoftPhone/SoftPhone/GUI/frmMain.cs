using SIPSorcery.SIP;
using SoftPhone.Helper;
using SoftPhone.SipSorcery;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftPhone.GUI
{
    public partial class frmMain : Form
    {
        private SIPTransportManager _sipTransportManager;
        private SipClient sipClients;
        string numberPhoneIncoming=null;
        private bool hold = false;
        private bool transfer = false;
        protected IEnumerable<Button> NumKeys;
        System.Timers.Timer timer;
        int timeCountSecond = 0;
        int timeCountMinute = 0;
        int timeCountHours = 0;
        ThreadClass threadClass = new ThreadClass();
        Utils utils = new Utils();
        public frmMain()
        {
            InitializeComponent();
            _sipTransportManager = new SIPTransportManager();
            _sipTransportManager.IncomingCall += SIPCallIncoming;
            sipClients = new SipClient(_sipTransportManager.SIPTransport);
            sipClients.CallAnswer += SipClientCallAnswer;
            sipClients.CallEnded += SipClientCallEnded;
            utils.CheckChangeMicrophone();
            sipClients.Register(_sipTransportManager.SIPTransport);
            
        }
        private void SipClientCallAnswer(SipClient obj)
        {           
            ClockTimer();
            ShowGrbCallWhenAnswer();
        }
        private void SipClientCallEnded(SipClient obj)
        {
            if(timer!=null)
            {
                timer.Stop();
                SetTimeCall();
            }
            utils.StopRing();
            threadClass.ChangeText(lblStatus, "Cuộc gọi kết thúc");
            Thread.Sleep(500);
            ShowGrbKeyboard();
        }
        private bool SIPCallIncoming(SIPRequest sipRequest)
        {
           
            if (!sipClients.IsCallActive)
            {
                numberPhoneIncoming = sipRequest.Header.From.FromName;
                utils.ShowMessageApp(ntfCall, 5000, "CUỘC GỌI ĐẾN", sipRequest.Header.From.FromName);
                threadClass.ChangeText(lbPhoneNumber, sipRequest.Header.From.FromName);
                threadClass.ChangeText(lblStatus, $"Đang gọi vào...");
                ShowGrbCallWhenIncoming();
                sipClients.Accept(sipRequest);
               
                return true;
            }
            else
            {
                return false;
            }
        }
        private void frmMain_Load(object sender, EventArgs e)
        {
            
            sipClients.StatusMessage += (client, message) => 
            { 
                threadClass.ChangeText(lblStatus, message); 
                if(message.Contains("Máy bận")|| message.Contains("Không khả dụng"))
                {
                    Thread.Sleep(1500);
                }    
            };           
            btnClear.Visible = false;
            GetMicroPhone();
            var numberKeys = new List<Button>();
            numberKeys.Add(btnKeypad0);
            numberKeys.Add(btnKeypad1);
            numberKeys.Add(btnKeypad2);
            numberKeys.Add(btnKeypad3);
            numberKeys.Add(btnKeypad4);
            numberKeys.Add(btnKeypad5);
            numberKeys.Add(btnKeypad6);
            numberKeys.Add(btnKeypad7);
            numberKeys.Add(btnKeypad8);
            numberKeys.Add(btnKeypad9);
            numberKeys.Add(btnKeypadAsterisk);
            numberKeys.Add(btnKeypadSharp);
            NumKeys = numberKeys;
            ShowRegister();
            
        }
        private void Keypad_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn != null)
            {
                string tag = btn.Tag.ToString().Trim();
                cbNumberPhone.Text += tag;
            }
            cbNumberPhone.Focus();
            cbNumberPhone.SelectionStart = cbNumberPhone.Text.Length;
        }
        private async void btnCall_Click(object sender, EventArgs e)
        {
           await Call();
        }
        public async Task Call()
        {
            if (!string.IsNullOrEmpty(cbNumberPhone.Text))
            {
                if (!utils.CheckAlreadyMicrophone())
                {
                    MessageBox.Show("Bạn chưa kết nối với Microphone.\nHãy kết nối với Microphone để tiếp tục cuộc gọi.", "Thông bóa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    threadClass.ChangeText(lbPhoneNumber, cbNumberPhone.Text);
                    if (!transfer)
                    {
                        utils.ShowMessageApp(ntfCall, 3000, "ĐANG GỌI RA", cbNumberPhone.Text);
                        await sipClients.Call(cbNumberPhone.Text.Trim());
                    }
                    else
                    {
                        await sipClients.BlindTransfer(cbNumberPhone.Text.Trim());
                        transfer = false;
                    }
                    ShowGrbCallWhenCallOut();
                }
            }
            else
            {
                MessageBox.Show("Bạn chưa nhập số điện thoại cần gọi!!!", "Thông báo",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
        }
        private void btnCancel_Click(object sender, EventArgs e)
        {
            sipClients.Cancel();
            ShowGrbKeyboard();
            threadClass.ChangeVisible(grbCall, false); 
        }
        private async void btnAnswer_Click(object sender, EventArgs e)
        {
            if (!utils.CheckAlreadyMicrophone())
            {
                MessageBox.Show("Bạn chưa kết nối với Microphone.\nHãy kết nối với Microphone để tiếp tục cuộc gọi.", "Thông bóa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                utils.StopRing();
                ClockTimer();
                await AnswerAsync();
                ShowGrbCallWhenAnswer();
                try
                {
                    Process process = new Process();
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.FileName = "chrome";
                    process.StartInfo.Arguments = $"https://sa.matbao.com/Support/CallNotes?uniqueid=IN&phone={numberPhoneIncoming}";
                    process.Start();
                }
                catch
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = $"https://sa.matbao.com/Support/CallNotes?uniqueid=IN&phone={numberPhoneIncoming}",
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
            }
        }
        private async Task AnswerAsync()
        {
            bool result = await sipClients.Answer();
        }
        private void btnExit_Click(object sender, EventArgs e)
        {
            sipClients.Exist();
            Application.Exit();
            
        }
        private void btnReject_Click(object sender, EventArgs e)
        {
            sipClients.Reject();
            threadClass.ChangeText(lblStatus, "Đã từ chối.");
            ShowGrbKeyboard();
            threadClass.ChangeVisible(grbCall, false);
        }

        private async void btnHold_Click(object sender, EventArgs e)
        {
            if(!hold)
            {
               await sipClients.PutOnHold();
                hold = true;
                threadClass.ChangeText(lbHold,"Thôi giữ máy");
                timer.Stop();
            }
            else
            {
                sipClients.TakeOffHold();
                hold = false;
                threadClass.ChangeText(lbHold, "Giữ máy");
                timer.Start();
            }
        }
        private void btnTransfer_Click(object sender, EventArgs e)
        {
            transfer = true;
            threadClass.ChangeVisible(grbCall,false);
            ShowGrbKeyboard();
        }
        private void btnHangUp_Click(object sender, EventArgs e)
        {
            sipClients.Hangup();
        }
        public void ShowRegister()
        {
            threadClass.ChangSizeText(lbTitle,(float)6.5);
            threadClass.ChangeLocationGroup(pnButtonSize, 183, 0);
            threadClass.ChangeVisible(grbCall, false);
            threadClass.ChangeVisible(grbKeyboard, false);
            threadClass.ChangeVisible(grbRegister, true);
            threadClass.ChangeSizeFrom(this, 247, 355);
            threadClass.ChangeLocationGroup(grbRegister, 2/*(grbRegister.Parent.Width-grbRegister.Width+2)/2*/, 26);
        }
        public void ShowGrbCallWhenIncoming()
        {
            threadClass.ChangeVisible(grbRegister, false);
            threadClass.ChangeLocationGroup(pnButtonSize, 211, 0);
            threadClass.ChangeSizeFrom(this, 283, 418);
            threadClass.ChangeLocationGroup(grbCall, 2, 22);
            utils.PlayRing("ring_incoming");
            threadClass.ChangeVisible(btnCancel, false);
            threadClass.ChangeVisible(pnHold, false);
            threadClass.ChangeVisible(pnTransfer, false);
            threadClass.ChangeVisible(pnKeyboard, false);
            threadClass.ChangeVisible(btnHangUp, false);
            threadClass.ChangeVisible(btnAnswer, true);
            threadClass.ChangeVisible(btnReject, true);
            threadClass.ChangeVisible(grbCall, true);
        }
        public void ShowGrbCallWhenAnswer()
        {
            threadClass.ChangeVisible(btnCancel, false);
            threadClass.ChangeVisible(btnAnswer, false);
            threadClass.ChangeVisible(btnReject, false);
            threadClass.ChangeVisible(pnHold,true);
            threadClass.ChangeVisible(pnKeyboard, true);
            threadClass.ChangeVisible(pnTransfer, true);
            threadClass.ChangeVisible(btnHangUp, true);
            threadClass.ChangeLocation(btnHangUp, 105, 325);
            threadClass.ChangeLocation(pnHold, 20, 222);
            threadClass.ChangeLocation(pnKeyboard, 105, 222);
            threadClass.ChangeLocation(pnTransfer, 175, 222);
        }
        public void ShowGrbCallWhenCallOut()
        {
            threadClass.ChangeLocationGroup(pnButtonSize, 211, 0);
            threadClass.ChangeSizeFrom(this, 283, 418);
            threadClass.ChangeLocationGroup(grbCall, 2, 22);
            threadClass.ChangeVisible(grbKeyboard, false);
            threadClass.ChangeVisible(btnAnswer, false);
            threadClass.ChangeVisible(pnHold, false);
            threadClass.ChangeVisible(pnTransfer, false);
            threadClass.ChangeVisible(pnKeyboard, false);
            threadClass.ChangeVisible(btnHangUp, false);
            threadClass.ChangeVisible(btnReject, false);
            threadClass.ChangeVisible(btnAnswer, false);
            threadClass.ChangeVisible(btnCancel, true);
            threadClass.ChangeLocation(btnCancel, 101, 230);
            threadClass.ChangeVisible(grbCall, true);
        }
        public void ShowGrbKeyboard()
        {
            threadClass.ChangeVisible(grbKeyboard, true);
            threadClass.ChangeLocationGroup(grbKeyboard, 2, 24);
            threadClass.ChangSizeText(lbTitle, 7);
            threadClass.ChangeLocationGroup(pnButtonSize, 214, 0);
            threadClass.ChangeVisible(grbRegister, false);
            threadClass.ChangeVisible(grbCall, false);
            threadClass.ChangeSizeFrom(this,287,480);
            threadClass.FocusControl(cbNumberPhone);
        }

        private void btnKeyboard_Click(object sender, EventArgs e)
        {
            ShowGrbKeyboard();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            if(cbNumberPhone.Text.Trim().Length>0)
            {
                char []arrNumber = cbNumberPhone.Text.Trim().ToCharArray();
                cbNumberPhone.Text = null;
                for(int i=0;i<arrNumber.Length-1;i++)
                {
                    cbNumberPhone.Text += arrNumber[i];
                }
                cbNumberPhone.SelectionStart = cbNumberPhone.Text.Length;
            }    
        }
        private void timer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (timeCountHours == 0)
            {
                if (timeCountSecond <= 59)
                {
                    threadClass.ChangeText(lblStatus, $"{ShowTime(timeCountMinute)}:{ShowTime(timeCountSecond)}");
                }
                else
                {
                    timeCountSecond = 0;
                    timeCountMinute++;
                    if (timeCountMinute <= 59)
                    {
                        threadClass.ChangeText(lblStatus, $"{ShowTime(timeCountMinute)}:{ShowTime(timeCountSecond)}");
                    }
                    else
                    {
                        timeCountMinute = 0;
                        timeCountHours++;
                        threadClass.ChangeText(lblStatus, $"{ShowTime(timeCountHours)}:{ShowTime(timeCountMinute)}:{ShowTime(timeCountSecond)}");
                    }
                }
            }
            else
            {
                if (timeCountSecond <= 59)
                {
                    threadClass.ChangeText(lblStatus, $"{ShowTime(timeCountHours)}:{ShowTime(timeCountMinute)}:{ShowTime(timeCountSecond)}");
                }
                else
                {
                    timeCountSecond = 0;
                    timeCountMinute++;
                    if (timeCountMinute <= 59)
                    {
                        threadClass.ChangeText(lblStatus, $"{ShowTime(timeCountHours)}:{ShowTime(timeCountMinute)}:{ShowTime(timeCountSecond)}");
                    }
                    else
                    {
                        timeCountMinute = 0;
                        timeCountHours++;
                        threadClass.ChangeText(lblStatus, $"{ShowTime(timeCountHours)}:{ShowTime(timeCountMinute)}:{ShowTime(timeCountSecond)}");
                    }
                }
            }
            timeCountSecond++;
        }
        public string ShowTime(int time)
        {
            string respon = string.Empty;
            if (time < 10)
            {
                respon = $"0{time}";
            }
            else
            {
                respon = $"{time}";
            }
            return respon;
        }
        public void SetTimeCall()
        {
            timeCountHours = 0;
            timeCountMinute = 0;
            timeCountSecond = 0;
        }
        public void ClockTimer()
        {
            timer = new System.Timers.Timer();
            timer.Interval = 1000;
            timer.Enabled = true;
            timer.Elapsed += timer_Tick;
        }
        private void cbNumberPhone_TextChanged(object sender, EventArgs e)
        {
            if(cbNumberPhone.Text.Trim().Length>0)
            {
                threadClass.ChangeVisible(btnClear,true);
            }
            else
            {
                threadClass.ChangeVisible(btnClear, false);
            }
        }
        private void GetMicroPhone()
        {
            if (Utils.GetMicrophone().Count != 0)
            {
                foreach (var item in Utils.GetMicrophone())
                {
                    cbSoundDrive.Items.Add(item);
                }
                cbSoundDrive.Text = Utils.GetMicrophone().FirstOrDefault();
            }
        }
        #region Mouse hover
        private void btnRegister_MouseHover(object sender, EventArgs e)
        {
            btnRegister.BackgroundImage = utils.GetResourceImage("bt_register_hover");
        }
        private void btnRegister_MouseLeave(object sender, EventArgs e)
        {
            btnRegister.BackgroundImage = utils.GetResourceImage("bt_register");
        }
        private void btnHold_MouseHover(object sender, EventArgs e)
        {
            btnHold.BackgroundImage = utils.GetResourceImage("bt_hold_hover");
        }
        private void btnHold_MouseLeave(object sender, EventArgs e)
        {
            btnHold.BackgroundImage = utils.GetResourceImage("bt_hold");
        }
        private void btnTransfer_MouseHover(object sender, EventArgs e)
        {
            btnTransfer.BackgroundImage = utils.GetResourceImage("bt_transfer_hover");
        }
        private void btnTransfer_MouseLeave(object sender, EventArgs e)
        {
            btnTransfer.BackgroundImage = utils.GetResourceImage("bt_transfer");
        }
        private void btnKeyboard_MouseHover(object sender, EventArgs e)
        {
            btnKeyboard.BackgroundImage = utils.GetResourceImage("keyboard_hover");
        }
        private void btnKeyboard_MouseLeave(object sender, EventArgs e)
        {
            btnKeyboard.BackgroundImage = utils.GetResourceImage("keyboard");
        }
        private void btnClear_MouseHover(object sender, EventArgs e)
        {
            btnKeyboard.BackgroundImage = utils.GetResourceImage("clear_pad_hover");
        }
        private void btnClear_MouseLeave(object sender, EventArgs e)
        {
            btnKeyboard.BackgroundImage = utils.GetResourceImage("clear_pad");
        }
        private void btnKeypad1_MouseHover(object sender, EventArgs e)
        {
            btnKeypad1.BackgroundImage = utils.GetResourceImage("p_1_hover");
        }
        private void btnKeypad1_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad1.BackgroundImage = utils.GetResourceImage("p_1");
        }
        private void btnKeypad2_MouseHover(object sender, EventArgs e)
        {
            btnKeypad2.BackgroundImage = utils.GetResourceImage("p_2_hover");
        }
        private void btnKeypad2_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad2.BackgroundImage = utils.GetResourceImage("p_2");
        }
        private void btnKeypad3_MouseHover(object sender, EventArgs e)
        {
            btnKeypad3.BackgroundImage = utils.GetResourceImage("p_3_hover");
        }
        private void btnKeypad3_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad3.BackgroundImage = utils.GetResourceImage("p_3");
        }
        private void btnKeypad4_MouseHover(object sender, EventArgs e)
        {
            btnKeypad4.BackgroundImage = utils.GetResourceImage("p_4_hover");
        }
        private void btnKeypad4_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad4.BackgroundImage = utils.GetResourceImage("p_4");
        }
        private void btnKeypad5_MouseHover(object sender, EventArgs e)
        {
            btnKeypad5.BackgroundImage = utils.GetResourceImage("p_5_hover");
        }
        private void btnKeypad5_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad5.BackgroundImage = utils.GetResourceImage("p_5");
        }
        private void btnKeypad6_MouseHover(object sender, EventArgs e)
        {
            btnKeypad6.BackgroundImage = utils.GetResourceImage("p_6_hover");
        }
        private void btnKeypad6_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad6.BackgroundImage = utils.GetResourceImage("p_6");
        }
        private void btnKeypad7_MouseHover(object sender, EventArgs e)
        {
            btnKeypad7.BackgroundImage = utils.GetResourceImage("p_7_hover");
        }
        private void btnKeypad7_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad7.BackgroundImage = utils.GetResourceImage("p_7");
        }
        private void btnKeypad8_MouseHover(object sender, EventArgs e)
        {
            btnKeypad8.BackgroundImage = utils.GetResourceImage("p_8_hover");
        }
        private void btnKeypad8_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad8.BackgroundImage = utils.GetResourceImage("p_8");
        }
        private void btnKeypad9_MouseHover(object sender, EventArgs e)
        {
            btnKeypad9.BackgroundImage = utils.GetResourceImage("p_9_hover");
        }
        private void btnKeypad9_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad9.BackgroundImage = utils.GetResourceImage("p_9");
        }
        private void btnKeypad0_MouseHover(object sender, EventArgs e)
        {
            btnKeypad0.BackgroundImage = utils.GetResourceImage("p_0_hover");
        }
        private void btnKeypad0_MouseLeave(object sender, EventArgs e)
        {
            btnKeypad0.BackgroundImage = utils.GetResourceImage("p_0");
        }        
        private void btnKeypadAsterisk_MouseLeave(object sender, EventArgs e)
        {
            btnKeypadAsterisk.BackgroundImage = utils.GetResourceImage("p_sao");
        }
        private void btnKeypadAsterisk_MouseHover(object sender, EventArgs e)
        {
            btnKeypadAsterisk.BackgroundImage = utils.GetResourceImage("p_sao_hover");
        }
        private void btnKeypadSharp_MouseHover(object sender, EventArgs e)
        {
            btnKeypadSharp.BackgroundImage = utils.GetResourceImage("p_sharp_hover");
        }
        private void btnKeypadSharp_MouseLeave(object sender, EventArgs e)
        {
            btnKeypadSharp.BackgroundImage = utils.GetResourceImage("p_sharp");
        }
        #endregion

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            
            ShowGrbKeyboard();
        }

        private async void cbNumberPhone_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode==Keys.Enter)
            {
                await Call();
            }    
        }
    }
}
