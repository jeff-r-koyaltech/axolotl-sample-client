using libaxolotl;
using libaxolotl.protocol;
using libaxolotl.state;
using libaxolotl.state.impl;
using libaxolotl.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace SampleClient
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Axolotl : Page
    {
        public Axolotl()
        {
            this.InitializeComponent();
        }

        #region "Address book"
        private const string alicePhoneNumber = "+18009925423"; //"1-800-99-ALICE"
        private const string bobPhoneNumber = "+18009999262"; //"1-800-9999-BOB"
        private const uint DEVICE_ID = 1; //for demo purposes, everyone's device ID is 1
        #endregion

        //sort of represents alice's device
        private CryptoState alice;
        //sort of represents bob's device
        private CryptoState bob;

        private AxolotlAddress aliceAddress;
        private AxolotlAddress bobAddress;

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            #region initialization
            txtBlock.Text = "";
            DateTime before = DateTime.UtcNow;
            AppendLine("Initializing...");
            await Task.Run(new Action(() => {
                alice = new CryptoState();
                bob = new CryptoState();
            }));
            DateTime after = DateTime.UtcNow;

            aliceAddress = new AxolotlAddress(alicePhoneNumber, DEVICE_ID);
            bobAddress = new AxolotlAddress(bobPhoneNumber, DEVICE_ID);

            PreKeyBundle bobsBundle = bob.GetMySignedPreKey();
            alice.SetupSession(bobAddress, bobsBundle);

            PreKeyBundle alicesBundle = alice.GetMySignedPreKey();
            bob.SetupSession(aliceAddress, alicesBundle);
            #endregion

            AppendLine("Timing: " + after.Subtract(before).TotalMilliseconds);

            //first message automatic, just for demonstration purposes
            #region Alice initiates a message to bob
            AppendLine("Alice initiates a chat session with bob...");
            txtMessage.Text = "Bob, what's up?";
            btnAliceSays_Click(sender, e);
            #endregion

        }

        private void btnAliceSays_Click(object sender, RoutedEventArgs e)
        {
            #region Alice sends
            string message = txtMessage.Text;
            CiphertextMessage ctMsg = alice.PrepareMessage(message);
            AppendLine("Alice sends: " + Convert.ToBase64String(ctMsg.serialize()));

            //==============================================
            //Alice sends the cipher text over the Internet
            //==============================================
            #endregion

            #region Bob receives
            string plainText = bob.ReceiveMessage(ctMsg);
            AppendLine("Bob got: " + plainText);
            #endregion
        }

        private void btnBobSays_Click(object sender, RoutedEventArgs e)
        {
            #region Bob sends
            string message = txtMessage.Text;
            CiphertextMessage ctMsg = bob.PrepareMessage(message);
            AppendLine("Bob sends: " + Convert.ToBase64String(ctMsg.serialize()));

            //==============================================
            //Alice sends the cipher text over the Internet
            //==============================================
            #endregion

            #region Alice receives
            string plainText = alice.ReceiveMessage(ctMsg);
            AppendLine("Alice got: " + plainText);
            #endregion
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.GoBack();
        }

        private void AppendLine(string message)
        {
            txtBlock.Text += message + Environment.NewLine;
        }
    }

    public class CryptoState
    {
        private const uint DEVICE_ID = 1; //for demo purposes, everyone's device ID is 1

        private IdentityKeyPair identityKeyPair;
        private uint registrationId;
        private PreKeyRecord lastResortKey;
        private SignedPreKeyRecord signedPreKey;
        private AxolotlStore axolotlStore;

        //Normally store these per remote-recipient, but for a demo, just alice and bob are talking
        private SessionBuilder sessionBuilder;
        private SessionCipher sessionCipher;

        public CryptoState()
        {
            identityKeyPair = KeyHelper.generateIdentityKeyPair();
            registrationId = KeyHelper.generateRegistrationId(false);
            lastResortKey = KeyHelper.generateLastResortPreKey();
            signedPreKey = KeyHelper.generateSignedPreKey(identityKeyPair, 5);//normally generate 100, but for a demo, 1 will do

            axolotlStore = new InMemoryAxolotlStore(identityKeyPair, registrationId);
            axolotlStore.StoreSignedPreKey(signedPreKey.getId(), signedPreKey);
        }

        /// <summary>
        /// Instead of calling a real server on the Internet, alice or bob can ask the each others' axolotlStore objects to give them a signed prekey.
        /// </summary>
        /// <returns>A signed pre key for THIS user</returns>
        public PreKeyBundle GetMySignedPreKey()
        {
            PreKeyBundle bundle = new PreKeyBundle(
                registrationId, DEVICE_ID,
                0, null,
                signedPreKey.getId(), signedPreKey.getKeyPair().getPublicKey(), signedPreKey.getSignature(),
                identityKeyPair.getPublicKey());
            return bundle;
        }

        /// <summary>
        /// Start a session for communicating with a remote party.
        /// </summary>
        /// <param name="remoteAddress">Remote party</param>
        /// <param name="preKey">Pre Key for that remote party, pulled from the server (or pulled from memory, like in this demo).</param>
        public void SetupSession(AxolotlAddress remoteAddress, PreKeyBundle preKey)
        {
            sessionBuilder = new SessionBuilder(axolotlStore, remoteAddress);
            sessionBuilder.process(preKey);
            sessionCipher = new SessionCipher(axolotlStore, remoteAddress);
        }

        /// <summary>
        /// Encrypt something for sending using the Axolotl ratchet.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>Cipher text equivalent</returns>
        public CiphertextMessage PrepareMessage(string message)
        {
            CiphertextMessage cipherMsg = sessionCipher.encrypt(Encoding.UTF8.GetBytes(message));
            return cipherMsg;
        }

        /// <summary>
        /// Decrypt something received by another party.
        /// </summary>
        /// <param name="ctMsg">Encrypted message (prekey, whisper message, etc?)</param>
        /// <returns>plain text of the message</returns>
        public string ReceiveMessage(CiphertextMessage ctMsg)
        {
            byte[] plainText = null;
            switch(ctMsg.getType())
            {
                case CiphertextMessage.PREKEY_TYPE:
                    plainText = sessionCipher.decrypt(ctMsg as PreKeyWhisperMessage);
                    break;
                case CiphertextMessage.WHISPER_TYPE:
                    plainText = sessionCipher.decrypt(ctMsg as WhisperMessage);
                    break;
            }
            return Encoding.UTF8.GetString(plainText);
        }
    }

}
