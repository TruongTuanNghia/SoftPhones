using SIPSorcery.Net;
using SIPSorcery.SIP;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SoftPhone.SipSorcery
{
    public class SIPTransportManager
    {
        private bool _isInitialised = false;
        private static int SIP_DEFAULT_PORT = SIPConstants.DEFAULT_SIP_PORT;
        private static string HOMER_SERVER_ADDRESS = null;
        private static int HOMER_SERVER_PORT = 9060;
        public SIPTransport SIPTransport { get; private set; }
        public event Func<SIPRequest, bool> IncomingCall;
        private UdpClient _homerSIPClient;
        public SIPTransportManager()
        {
            if (HOMER_SERVER_ADDRESS != null)
            {
                _homerSIPClient = new UdpClient(0, AddressFamily.InterNetwork);
            }
            InitialiseSIP();
        }
        public void Shutdown()
        {
            if (SIPTransport != null)
            {
                SIPTransport.Shutdown();
            }
        }
        public void InitialiseSIP()
        {
            SIPTransport = new SIPTransport();
            SIPTransport.SIPTransportRequestReceived += SIPTransportRequestReceived;
            SIPTransport.SIPRequestInTraceEvent += SIPRequestInTraceEvent;
            SIPTransport.SIPRequestOutTraceEvent += SIPRequestOutTraceEvent;
            SIPTransport.SIPResponseInTraceEvent += SIPResponseInTraceEvent;
            SIPTransport.SIPResponseOutTraceEvent += SIPResponseOutTraceEvent;
        }
        private Task SIPTransportRequestReceived(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            if (sipRequest.Method == SIPMethodsEnum.INFO)
            {
                SIPResponse notAllowedResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
                return SIPTransport.SendResponseAsync(notAllowedResponse);
            }
            else if (sipRequest.Method == SIPMethodsEnum.INVITE)
            {
                bool? callAccepted = IncomingCall?.Invoke(sipRequest);

                if (callAccepted == false)
                {
                    UASInviteTransaction uasTransaction = new UASInviteTransaction(SIPTransport, sipRequest, null);
                    SIPResponse busyResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.BusyHere, null);
                    uasTransaction.SendFinalResponse(busyResponse);
                }
            }
            else
            {
                SIPResponse notAllowedResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
                return SIPTransport.SendResponseAsync(notAllowedResponse);
            }
            return Task.FromResult(0);
        }
        private void SIPRequestInTraceEvent(SIPEndPoint localEP, SIPEndPoint remoteEP, SIPRequest sipRequest)
        {
            if (_homerSIPClient != null)
            {
                var hepBuffer = HepPacket.GetBytes(remoteEP, localEP, DateTime.Now, 333, "myHep", sipRequest.ToString());
                _homerSIPClient.SendAsync(hepBuffer, hepBuffer.Length, HOMER_SERVER_ADDRESS, HOMER_SERVER_PORT);
            }
        }
        private void SIPRequestOutTraceEvent(SIPEndPoint localEP, SIPEndPoint remoteEP, SIPRequest sipRequest)
        {
            if (_homerSIPClient != null)
            {
                var hepBuffer = HepPacket.GetBytes(localEP, remoteEP, DateTime.Now, 333, "myHep", sipRequest.ToString());
                _homerSIPClient.SendAsync(hepBuffer, hepBuffer.Length, HOMER_SERVER_ADDRESS, HOMER_SERVER_PORT);
            }
        }
        private void SIPResponseInTraceEvent(SIPEndPoint localEP, SIPEndPoint remoteEP, SIPResponse sipResponse)
        {
            if (_homerSIPClient != null)
            {
                var hepBuffer = HepPacket.GetBytes(remoteEP, localEP, DateTime.Now, 333, "myHep", sipResponse.ToString());
                _homerSIPClient.SendAsync(hepBuffer, hepBuffer.Length, HOMER_SERVER_ADDRESS, HOMER_SERVER_PORT);
            }
        }
        private void SIPResponseOutTraceEvent(SIPEndPoint localEP, SIPEndPoint remoteEP, SIPResponse sipResponse)
        {
            if (_homerSIPClient != null)
            {
                var hepBuffer = HepPacket.GetBytes(localEP, remoteEP, DateTime.Now, 333, "myHep", sipResponse.ToString());
                _homerSIPClient.SendAsync(hepBuffer, hepBuffer.Length, HOMER_SERVER_ADDRESS, HOMER_SERVER_PORT);
            }
        }
    }
}
