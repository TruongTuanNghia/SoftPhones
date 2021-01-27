using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SoftPhone.SipSorcery
{  
    public class SipClient
    {
        private static int TRANSFER_RESPONSE_TIMEOUT_SECONDS = 10;
        private SIPTransport sipTransport;
        SIPRegistrationUserAgent registrationClient;
        private SIPUserAgent userAgent;
        private SIPServerUserAgent pendingIncomingCall;
        private CancellationTokenSource cts = new CancellationTokenSource();
        private int m_audioOutDeviceIndex = -1;
        public event Action<SipClient> CallAnswer;                 
        public event Action<SipClient> CallEnded;                  
        public event Action<SipClient, string> StatusMessage;      
        public event Action<SipClient> RemotePutOnHold;            
        public event Action<SipClient> RemoteTookOffHold;
        public SIPDialogue Dialogue
        {
            get { return userAgent.Dialogue; }
        }
        public VoIPMediaSession MediaSession { get; private set; }
        private VoIPMediaSession CreateMediaSession()//Kết nối micro
        {
            var windowsAudioEndPoint = new WindowsAudioEndPoint(new AudioEncoder(), m_audioOutDeviceIndex);
            MediaEndPoints mediaEndPoints = new MediaEndPoints
            {
                AudioSink = windowsAudioEndPoint,
                AudioSource = windowsAudioEndPoint,
            };
            // Fallback video source if a Windows webcam cannot be accessed.

            var voipMediaSession = new VoIPMediaSession(mediaEndPoints, null);
            voipMediaSession.AcceptRtpFromAny = true;

            return voipMediaSession;
        }
        public bool IsCallActive
        {
            get { return userAgent.IsCallActive; }
        }
        public bool IsOnHold
        {
            get { return userAgent.IsOnLocalHold || userAgent.IsOnRemoteHold; }
        }
        public SipClient(SIPTransport sipTransport)
        {
            this.sipTransport = sipTransport;
            userAgent = new SIPUserAgent(this.sipTransport, null,true);
            userAgent.ClientCallTrying += CallTrying;
            userAgent.ClientCallRinging += CallRinging;
            userAgent.ClientCallAnswered += CallAnswered;
            userAgent.ClientCallFailed += CallFailed;
            userAgent.OnCallHungup += CallFinished;
            userAgent.ServerCallCancelled += IncomingCallCancelled;
            userAgent.OnTransferNotify += OnTransferNotify;
           // Register(sipTransport);
        }
        public void Register(SIPTransport sipTransport)
        {
            registrationClient = new SIPRegistrationUserAgent(sipTransport, "2230229769", "2571621234", "sip.tel4vn.com", 120);
            registrationClient.Start();
        }
        public string UriPhoneNumber(string phoneNumber)
        {
            return $"sip:{phoneNumber}@sip.tel4vn.com:50061;user=phone";
        }
        public async Task Call(string phoneNumber)
        {
            var offerSDP = CreateMediaSession();
            //string callTo = $"sip:{phoneNumber}@125.253.123.195:5060;user=phone";
            string callTo = UriPhoneNumber(phoneNumber);
            SIPURI callUri = SIPURI.ParseSIPURIRelaxed(callTo);
            SIPCallDescriptor callDescriptor = new SIPCallDescriptor(
              "2230229769",
              "2571621234",
              callUri.ToString(),
              "sip:2230229769@sip.tel4vn.com:50061",
              callTo,
              null, null, null,
              SIPCallDirection.Out,
              SDP.SDP_MIME_CONTENTTYPE,
              offerSDP.ToString(),
              null
              );
            MediaSession = CreateMediaSession();
            await userAgent.InitiateCallAsync(callDescriptor, MediaSession);
        }
        public void Cancel()
        {
            StatusMessage(this, "Cancelling SIP call to " + userAgent.CallDescriptor?.Uri + ".");
            userAgent.Cancel();
        }
        public void Accept(SIPRequest sipRequest)//Chấp nhận cuộc gọi đến
        {
            pendingIncomingCall = userAgent.AcceptCall(sipRequest);
        }
        
        public async Task<bool> Answer()//nghe cuộc gọi đến
        {
            if (pendingIncomingCall == null)
            {
                StatusMessage(this, $"Không có cuộc gọi tới nào để trả lời.");
                return false;
            }
            else
            {
                var sipRequest = pendingIncomingCall.ClientTransaction.TransactionRequest;
                bool hasAudio = true;
                bool hasVideo = false;
                if (sipRequest.Body != null)
                {
                    SDP offerSDP = SDP.ParseSDPDescription(sipRequest.Body);
                    hasAudio = offerSDP.Media.Any(x => x.Media == SDPMediaTypesEnum.audio && x.MediaStreamStatus != MediaStreamStatusEnum.Inactive);
                    hasVideo = offerSDP.Media.Any(x => x.Media == SDPMediaTypesEnum.video && x.MediaStreamStatus != MediaStreamStatusEnum.Inactive);
                }
                MediaSession = CreateMediaSession();
                userAgent.RemotePutOnHold += OnRemotePutOnHold;
                userAgent.RemoteTookOffHold += OnRemoteTookOffHold;
                bool result = await userAgent.Answer(pendingIncomingCall, MediaSession);
                pendingIncomingCall = null;
                 return result;
            }
        }
        public void Redirect(string destination)//Chuyển cuộc gọi
        {
            pendingIncomingCall?.Redirect(SIPResponseStatusCodesEnum.MovedTemporarily, SIPURI.ParseSIPURIRelaxed(destination));
        }
        public async Task PutOnHold()//Giữ máy
        {
            await MediaSession.PutOnHold();
            userAgent.PutOnHold();
            StatusMessage(this, "Đang giữ cuộc gọi");
        }
        public void TakeOffHold()//Thôi giữ máy
        {
            MediaSession.TakeOffHold();
            userAgent.TakeOffHold();
            //StatusMessage(this, "Thôi giữ cuộc gọi");
        }
        public void Reject()//Từ chối nghe máy
        {
            pendingIncomingCall?.Reject(SIPResponseStatusCodesEnum.BusyHere, null, null);
        }
        public void Hangup()//cúp máy khi cuộc gọi thành công
        {
            if (userAgent.IsCallActive)
            {
                userAgent.Hangup();
                CallFinished(null);
            }
        }
        public Task<bool> BlindTransfer(string phoneNumber)//chuyển cuộc gọi
        {
            string uriTransfer = UriPhoneNumber(phoneNumber);
            if (SIPURI.TryParse(uriTransfer, out var uri))
            {
                return userAgent.BlindTransfer(uri, TimeSpan.FromSeconds(TRANSFER_RESPONSE_TIMEOUT_SECONDS), cts.Token);
            }
            else
            {
                StatusMessage(this, $"Không thể chuyển cuộc gọi.");
                return Task.FromResult(false);
            }
        }
        public Task<bool> AttendedTransfer(SIPDialogue transferee)
        {
            return userAgent.AttendedTransfer(transferee, TimeSpan.FromSeconds(TRANSFER_RESPONSE_TIMEOUT_SECONDS), cts.Token);
        }
        public void Shutdown()
        {
            Hangup();
        }
        private void CallTrying(ISIPClientUserAgent uac, SIPResponse sipResponse)
        {
            StatusMessage(this,"Đang thiết lập cuộc gọi");
        }
        private void CallRinging(ISIPClientUserAgent uac, SIPResponse sipResponse)
        {
            StatusMessage(this, "Đang gọi ra...");
        }
        private void CallFailed(ISIPClientUserAgent uac, string errorMessage, SIPResponse failureResponse)
        {
            if(errorMessage.Contains("Busy Here"))
            {
                StatusMessage(this, "Máy bận");
            }
            else if(errorMessage.Contains("Temporarily unavailable"))
            {
                StatusMessage(this, "Không khả dụng");
            }  else   
            StatusMessage(this, "Cuộc gọi phát sinh lỗi: " + errorMessage + ".");
            CallFinished(Dialogue);
        }
        private void CallAnswered(ISIPClientUserAgent uac, SIPResponse sipResponse)
        {
            StatusMessage(this, "Đang trong cuộc gọi");
            CallAnswer?.Invoke(this);
        }
        private void CallFinished(SIPDialogue dialogue)
        {
            pendingIncomingCall = null;
            CallEnded(this);
        }
        private void IncomingCallCancelled(ISIPServerUserAgent uas)
        {
            CallFinished(null);
        }
        private void OnTransferNotify(string sipFrag)
        {
            if (sipFrag?.Contains("SIP/2.0 200") == true)
            {
                Hangup();
            }
            else
            {
                Match statusCodeMatch = Regex.Match(sipFrag, @"^SIP/2\.0 (?<statusCode>\d{3})");
                if (statusCodeMatch.Success)
                {
                    int statusCode = Int32.Parse(statusCodeMatch.Result("${statusCode}"));
                    SIPResponseStatusCodesEnum responseStatusCode = (SIPResponseStatusCodesEnum)statusCode;
                    StatusMessage(this, $"Chuyển cuộc gọi thất bại {responseStatusCode}");
                }
            }
        }
        private void OnRemotePutOnHold()
        {
            RemotePutOnHold?.Invoke(this);
        }
        private void OnRemoteTookOffHold()
        {
            RemoteTookOffHold?.Invoke(this);
        }
        public void Exist()
        {
            if(registrationClient!=null)
            registrationClient.Stop();
        }
    }
}
